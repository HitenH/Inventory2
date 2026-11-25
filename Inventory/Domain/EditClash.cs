using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Inventory.Domain;

/// <summary>
/// Version 3.0, batch 1c-2: "someone else changed this, reload" instead of a silent overwrite.
/// Each browser tab remembers which version (the "changed at" stamp) of a record it opened for editing.
/// At every save <see cref="RecordStamps"/> compares that with the database; if another person (or another tab)
/// saved the record in between, the save is refused with <see cref="RecordChangedException"/> and nothing is written.
/// Two people working on different records never meet this.
/// </summary>
public sealed class SeenVersions
{
    private readonly Dictionary<string, DateTime?> seen = new();
    private readonly object gate = new();

    /// <summary>Raised when a save from this tab is refused, so the layout can offer "Reload".</summary>
    public event Action<RecordChangedException>? Clash;

    public void Record(string key, DateTime? changedAt)
    {
        lock (gate) { seen[key] = changedAt; }
    }

    public bool TryGet(string key, out DateTime? changedAt)
    {
        lock (gate) { return seen.TryGetValue(key, out changedAt); }
    }

    public bool Contains(string key)
    {
        lock (gate) { return seen.ContainsKey(key); }
    }

    public void Forget(string key)
    {
        lock (gate) { seen.Remove(key); creating.Remove(key); }
    }

    // Batch 1c-4 (owner, 25 Nov 2026): lines added to a new record before leaving its screen count as creating it,
    // so the activity log folds them into the record's "Created" entry instead of listing them one by one.
    private readonly HashSet<string> creating = new();

    /// <summary>This tab has just created the record: until the next screen change, additions to it are part of creating it.</summary>
    public void MarkCreating(string key)
    {
        lock (gate) { creating.Add(key); }
    }

    public bool IsCreating(string key)
    {
        lock (gate) { return creating.Contains(key); }
    }

    /// <summary>Another screen was opened: whatever was being created here is now created, and every sitting ends.</summary>
    public void EndCreating()
    {
        lock (gate) { creating.Clear(); sittings.Clear(); }
    }

    // Owner, 25 Nov 2026: changes one person makes to one record in one sitting (same screen, no pause longer than
    // SittingPause) are one entry in the activity log, so lines added one by one fold like a Bulk Entry.
    public static readonly TimeSpan SittingPause = TimeSpan.FromMinutes(30);
    private readonly Dictionary<string, (Guid Id, DateTime Last)> sittings = new();

    /// <summary>The activity-log group for a change to this record now: the current sitting's, or a new one.</summary>
    public Guid SittingFor(string key, DateTime now)
    {
        lock (gate)
        {
            var id = sittings.TryGetValue(key, out var s) && now - s.Last <= SittingPause ? s.Id : Guid.NewGuid();
            sittings[key] = (id, now);
            return id;
        }
    }

    internal void Raise(RecordChangedException clash) => Clash?.Invoke(clash);

    /// <summary>"PurchaseEntity:3f2a…" – the record's kind and key.</summary>
    public static string KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()!;
        return entry.Metadata.ClrType.Name + ":" + string.Join("|", key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
    }

    /// <summary>"PurchaseEntity:3f2a…" for an entity of this kind and key.</summary>
    public static string KeyOf<T>(object id) => typeof(T).Name + ":" + id;
}

/// <summary>The save was refused because the record changed (or went) since this tab opened it. The message is for the operator.</summary>
public sealed class RecordChangedException(string message) : Exception(message)
{
    /// <summary>Plain name of a kind of record, as the operator says it.</summary>
    public static string KindOf(Type type) => type.Name switch
    {
        nameof(CustomerEntity) => "customer",
        nameof(SupplierEntity) => "supplier",
        nameof(ProductEntity) => "product",
        nameof(CategoryEntity) => "category",
        nameof(PurchaseOrderEntity) => "purchase order",
        nameof(PurchaseEntity) => "purchase",
        nameof(SalesOrderEntity) => "sales order",
        nameof(SalesEntity) => "sale",
        nameof(UserEntity) => "user",
        nameof(RoleEntity) => "role",
        nameof(PurchaseVariant) or nameof(SalesOrderVariantEntity) or nameof(SalesVariantEntity) => "line",
        nameof(VariantEntity) => "variant",
        _ => "record",
    };

    public static RecordChangedException Changed(Type type, string? by, DateTime? at, DateTime now)
    {
        var who = string.IsNullOrWhiteSpace(by) ? "Someone else" : by;
        var when = at is not { } time ? ""
            : time.Date == now.Date ? $" at {time:HH:mm}"
            : $" on {time:dd-MM-yyyy} at {time:HH:mm}";
        return new($"{who} changed this {KindOf(type)}{when} while you had it open. Reload to see their changes.");
    }

    public static RecordChangedException Gone(Type type) =>
        new($"This {KindOf(type)} was deleted by someone else while you had it open. Reload to see what is there now.");
}

/// <summary>Helpers the repositories use to say "this tab opened these records for editing".</summary>
public static class SeenVersionsExtensions
{
    /// <summary>Remembers the version of each record as it was loaded, for the save-time check. Safe on untracked records.</summary>
    public static void MarkSeen(this DbContext context, IEnumerable<IStamped> records)
    {
        var seen = context.Seen();
        if (seen is null)
        {
            return;
        }

        foreach (var record in records)
        {
            seen.Record(SeenVersions.KeyOf(context.Entry(record)), record.ChangedAt);
        }
    }

    public static void MarkSeen(this DbContext context, IStamped? record)
    {
        if (record is not null)
        {
            context.MarkSeen([record]);
        }
    }

    /// <summary>A record this tab opened is no longer in the database: refuse, with a plain message.</summary>
    public static void ThrowIfGoneWhileOpen<T>(this DbContext context, object id)
    {
        var seen = context.Seen();
        if (seen is not null && seen.Contains(SeenVersions.KeyOf<T>(id)))
        {
            var gone = RecordChangedException.Gone(typeof(T));
            seen.Raise(gone);
            throw gone;
        }
    }

    /// <summary>A part (line, variant) this tab showed is no longer in the database.</summary>
    public static void ThrowPartGone<T>(this DbContext context)
    {
        var gone = RecordChangedException.Gone(typeof(T));
        context.Seen()?.Raise(gone);
        throw gone;
    }

    internal static SeenVersions? Seen(this DbContext context) =>
        context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()?
            .Interceptors?.OfType<RecordStampInterceptor>().FirstOrDefault()?.Seen;
}
