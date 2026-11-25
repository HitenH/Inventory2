using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Domain;

/// <summary>
/// Version 3.0, batch 1c-1: the one place that writes "created by / changed by / when".
/// Runs inside every save, so no screen, import or future device can forget it.
/// A change to a part of a record (a purchase line, a variant, a mobile number, a role's switches,
/// a user's roles) counts as a change to the record it belongs to.
/// The time comes from this PC's clock (one clock for everyone, ROADMAP 3.3).
/// Batch 1c-2: before stamping, each changed or deleted record is checked against the version this tab
/// opened (<see cref="SeenVersions"/>); a record someone else saved in between refuses the whole save.
/// </summary>
public static class RecordStamps
{
    /// <summary>Stamps the pending changes and returns the records it stamped (for <see cref="AfterSave"/>).</summary>
    public static List<EntityEntry> Apply(DbContext context, string? who, DateTime now, SeenVersions? seen = null)
    {
        var entries = context.ChangeTracker.Entries().ToList();
        var touched = new List<EntityEntry>();
        var done = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var entry in entries)
        {
            if (entry.Entity is not IStamped stamped)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                stamped.CreatedAt = now;
                stamped.CreatedBy = who;
                touched.Add(entry);
                done.Add(stamped);
            }
            else if (entry.State == EntityState.Modified)
            {
                // The created stamp is never rewritten by a later save (a screen that sends the whole record back
                // without these fields must not wipe them), real change or not.
                entry.Property(nameof(IStamped.CreatedAt)).IsModified = false;
                entry.Property(nameof(IStamped.CreatedBy)).IsModified = false;
                if (!HasRealChange(entry))
                {
                    entry.Property(nameof(IStamped.ChangedAt)).IsModified = false;
                    entry.Property(nameof(IStamped.ChangedBy)).IsModified = false;
                    continue;
                }

                CheckNotChangedSinceSeen(entry, seen, now);
                MarkChanged(entry, stamped, who, now);
                touched.Add(entry);
                done.Add(stamped);
            }
            else if (entry.State == EntityState.Deleted)
            {
                CheckNotChangedSinceSeen(entry, seen, now);
                touched.Add(entry);
                done.Add(stamped);
            }
        }

        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) || entry.Entity is IStamped)
            {
                continue;
            }

            var owner = FindOwner(context, entry);
            if (owner is null || done.Contains(owner))
            {
                continue;
            }

            var ownerEntry = context.Entry(owner);
            if (ownerEntry.State is EntityState.Added or EntityState.Deleted)
            {
                continue;
            }

            CheckNotChangedSinceSeen(ownerEntry, seen, now);
            MarkChanged(ownerEntry, (IStamped)owner, who, now);
            touched.Add(ownerEntry);
            done.Add(owner);
        }

        return touched;
    }

    /// <summary>After a successful save: this tab now holds the versions it has just written.</summary>
    public static void AfterSave(IEnumerable<EntityEntry> touched, SeenVersions? seen)
    {
        if (seen is null)
        {
            return;
        }

        foreach (var entry in touched)
        {
            var key = SeenVersions.KeyOf(entry);
            if (entry.State == EntityState.Detached)
            {
                // Deleted and saved: nothing left to remember.
                seen.Forget(key);
            }
            else if (entry.Entity is IStamped stamped)
            {
                // A record created here is remembered too, so a later edit on the same screen is checked.
                seen.Record(key, stamped.ChangedAt);
            }
        }
    }

    /// <summary>The version in the database must still be the one this tab opened (if it opened it at all).</summary>
    private static void CheckNotChangedSinceSeen(EntityEntry entry, SeenVersions? seen, DateTime now)
    {
        if (seen is null || !seen.TryGet(SeenVersions.KeyOf(entry), out var opened))
        {
            return;
        }

        // Records are loaded fresh by the repositories, so the original values are what the database holds now.
        var current = (DateTime?)entry.Property(nameof(IStamped.ChangedAt)).OriginalValue;
        if (current == opened)
        {
            return;
        }

        var clash = RecordChangedException.Changed(entry.Metadata.ClrType, (string?)entry.Property(nameof(IStamped.ChangedBy)).OriginalValue, current, now);
        seen.Raise(clash);
        throw clash;
    }

    private static void MarkChanged(EntityEntry entry, IStamped stamped, string? who, DateTime now)
    {
        stamped.ChangedAt = now;
        stamped.ChangedBy = who;
        // Say so explicitly: an owner loaded just now is "unchanged", and change detection may already have run.
        entry.Property(nameof(IStamped.ChangedAt)).IsModified = true;
        entry.Property(nameof(IStamped.ChangedBy)).IsModified = true;
        entry.Property(nameof(IStamped.CreatedAt)).IsModified = false;
        entry.Property(nameof(IStamped.CreatedBy)).IsModified = false;
    }

    /// <summary>True when a value other than the stamps themselves is marked as changed.</summary>
    private static bool HasRealChange(EntityEntry entry) =>
        entry.Properties.Any(p => p.IsModified && !IsStampProperty(p.Metadata.Name));

    private static bool IsStampProperty(string name) =>
        name is nameof(IStamped.CreatedAt) or nameof(IStamped.CreatedBy) or nameof(IStamped.ChangedAt) or nameof(IStamped.ChangedBy);

    /// <summary>The stamped record a part belongs to, or null (pair rules and unknown parts have none).</summary>
    internal static object? FindOwner(DbContext context, EntityEntry entry)
    {
        object? Load<T>(object? key) where T : class => key is null ? null : context.Set<T>().Find(key);

        // For a deleted part the foreign key is still in the original values.
        object? Key(string property) => entry.State == EntityState.Deleted
            ? entry.Property(property).OriginalValue
            : entry.Property(property).CurrentValue;

        switch (entry.Entity)
        {
            case PurchaseVariant:
                return Load<PurchaseEntity>(Key(nameof(PurchaseVariant.PurchaseEntityId)));
            case SalesOrderVariantEntity:
                return Load<SalesOrderEntity>(Key(nameof(SalesOrderVariantEntity.SalesOrderEntityId)));
            case SalesVariantEntity:
                return Load<SalesEntity>(Key(nameof(SalesVariantEntity.SalesEntityId)));
            case SalesSummaryEntity:
                return Load<SalesEntity>(Key(nameof(SalesSummaryEntity.SalesEntityId)));
            case VariantEntity:
                return Load<ProductEntity>(Key(nameof(VariantEntity.ProductEntityId)));
            case Image:
                return Load<VariantEntity>(Key(nameof(Image.VariantEntityId))) is VariantEntity variant
                    ? Load<ProductEntity>(variant.ProductEntityId)
                    : null;
            case Mobile:
                return Load<CustomerEntity>(Key(nameof(Mobile.CustomerEntityId)))
                    ?? Load<SupplierEntity>(Key(nameof(Mobile.SupplierEntityId)));
            case RolePermissionEntity:
                return Load<RoleEntity>(Key(nameof(RolePermissionEntity.RoleId)));
            case UserRoleEntity:
                return Load<UserEntity>(Key(nameof(UserRoleEntity.UserId)));
            default:
                return null;
        }
    }
}

/// <summary>Who is logged in on this browser tab. Filled by <see cref="Inventory.Service.AccessService"/> on every screen change.</summary>
public sealed class CurrentUser
{
    public string? Name { get; set; }
}

/// <summary>
/// Calls <see cref="RecordStamps.Apply"/> before every save of a context made for this browser tab,
/// and <see cref="RecordStamps.AfterSave"/> after it succeeds. One per tab (the factory is scoped).
/// </summary>
public sealed class RecordStampInterceptor(CurrentUser currentUser, SeenVersions? seen = null) : SaveChangesInterceptor
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, Pending> pending = new();

    public SeenVersions? Seen { get; } = seen;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Before(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Before(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        After(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        After(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    private void Before(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTime.Now;
        var touched = RecordStamps.Apply(context, currentUser.Name, now, Seen);
        // Batch 1c-3: the activity log lines go into the same save (all or nothing with the data).
        context.AddRange(ActivityLogWriter.Describe(context, currentUser.Name, now, Seen));
        pending.AddOrUpdate(context, new Pending(touched, touched.Where(e => e.State == EntityState.Added).ToList()));
    }

    private void After(DbContext? context)
    {
        if (context is not null && pending.TryGetValue(context, out var saved))
        {
            pending.Remove(context);
            RecordStamps.AfterSave(saved.Touched, Seen);
            // Batch 1c-4: a record created from this tab is "being created" until the next screen change
            // (its key is known only now, after the database gave it one).
            foreach (var entry in saved.Created)
            {
                Seen?.MarkCreating(SeenVersions.KeyOf(entry));
            }
        }
    }

    private sealed record Pending(List<EntityEntry> Touched, List<EntityEntry> Created);

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            pending.Remove(context);
        }
    }
}
