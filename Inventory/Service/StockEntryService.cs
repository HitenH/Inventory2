using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>Stage 2, batch 2-7: what an entry on the Stock changes screen does.</summary>
public enum StockEntryType
{
    /// <summary>Damage found in the shop: Sellable → Damaged (it then shows on the damage report).</summary>
    DamagedInShop,
    /// <summary>A count found more (+) or fewer (−) pieces than the app: Outside → Sellable or Sellable → Outside.</summary>
    CountCorrection,
    /// <summary>Good stock thrown away or lost: Sellable → Outside.</summary>
    WrittenOff,
    /// <summary>Stock that was there before the app knew of it: Outside → Sellable.</summary>
    OpeningStock,
}

/// <summary>One row of the Stock changes list.</summary>
public sealed record StockEntryListItem(Guid Id, int VoucherId, DateOnly Date, StockEntryType Type, string? Reason, int Lines, int Pieces, StockChangeStatus Status, string? Remarks);

/// <summary>One stock change waiting for approval, as the "Waiting for approval" list shows it.</summary>
public sealed record WaitingEntry(Guid Id, string Label, string? Party, DateTime? At, string? By, IReadOnlyList<string> Lines, string? Link, string? Remarks);

/// <summary>One reason of the reasons list (Settings).</summary>
public sealed record ReasonItem(int Id, string Name, bool IsActive, int SortOrder);

/// <summary>
/// Version 3.0, stage 2, batches 2-7 and 2-8: the Stock changes screen (damage found in the shop, count corrections,
/// write-offs, opening stock), the reasons list, and approval – the Admin's switch and approving or rejecting what waits.
/// </summary>
public class StockEntryService(IDbContextFactory<AppDbContext> factory, ActivityLogService activity)
{
    public static string TypeName(StockEntryType type) => type switch
    {
        StockEntryType.DamagedInShop => "Damaged in the shop",
        StockEntryType.CountCorrection => "Count correction",
        StockEntryType.WrittenOff => "Written off",
        StockEntryType.OpeningStock => "Opening stock",
        _ => type.ToString(),
    };

    public static StockChangeKind KindOf(StockEntryType type) => type switch
    {
        StockEntryType.DamagedInShop => StockChangeKind.Damage,
        StockEntryType.CountCorrection => StockChangeKind.CountCorrection,
        StockEntryType.WrittenOff => StockChangeKind.WriteOff,
        _ => StockChangeKind.OpeningStock,
    };

    /// <summary>The type of an entry of this screen, or null for a stock change made elsewhere (a purchase's damage, the damage report).</summary>
    public static StockEntryType? TypeOf(StockChangeEntity entry) => entry.Kind switch
    {
        StockChangeKind.Damage when entry.PurchaseEntityId is null && entry.SourceStockChangeId is null => StockEntryType.DamagedInShop,
        StockChangeKind.CountCorrection => StockEntryType.CountCorrection,
        StockChangeKind.WriteOff when entry.Lines.All(l => l.From == StockState.Sellable) => StockEntryType.WrittenOff,
        StockChangeKind.OpeningStock => StockEntryType.OpeningStock,
        _ => null,
    };

    /// <summary>Where a line of this type moves its pieces. A count correction goes in when the typed quantity is above 0, out when below.</summary>
    public static (StockState From, StockState To) MoveOf(StockEntryType type, int quantity) => type switch
    {
        StockEntryType.DamagedInShop => (StockState.Sellable, StockState.Damaged),
        StockEntryType.CountCorrection => quantity > 0 ? (StockState.Outside, StockState.Sellable) : (StockState.Sellable, StockState.Outside),
        StockEntryType.WrittenOff => (StockState.Sellable, StockState.Outside),
        _ => (StockState.Outside, StockState.Sellable),
    };

    /// <summary>The quantity as the line editor shows it: a count correction that took pieces out is shown below 0.</summary>
    public static int Shown(StockChangeLineEntity line) => line.From == StockState.Sellable && line.To == StockState.Outside && line.StockChange?.Kind == StockChangeKind.CountCorrection
        ? -line.Quantity : line.Quantity;

    /// <summary>What is wrong with a typed quantity for this type, or null.</summary>
    public static string? QuantityProblem(StockEntryType type, int? quantity) => type == StockEntryType.CountCorrection
        ? (quantity is null or 0 ? "Type how many more (for example 3) or fewer (for example -2) the count found." : null)
        : (quantity is null or <= 0 ? "The quantity must be more than 0." : null);

    // ---------------------------------------------------------------- the Stock changes screen (batch 2-7)

    public async Task<List<StockEntryListItem>> EntriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var kinds = new[] { StockChangeKind.Damage, StockChangeKind.CountCorrection, StockChangeKind.WriteOff, StockChangeKind.OpeningStock };
        var rows = await db.StockChanges.AsNoTracking().Include(s => s.Lines)
            .Where(s => kinds.Contains(s.Kind) && s.PurchaseEntityId == null && s.SourceStockChangeId == null)
            .ToListAsync();
        return rows.Select(s => (Entry: s, Type: TypeOf(s)))
            .Where(x => x.Type is not null)
            .Select(x => new StockEntryListItem(x.Entry.Id, x.Entry.VoucherId, x.Entry.Date, x.Type!.Value, x.Entry.Reason, x.Entry.Lines.Count,
                x.Entry.Lines.Sum(l => l.Quantity), x.Entry.Status, x.Entry.Remarks))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.VoucherId)
            .ToList();
    }

    /// <summary>An entry of this screen with its lines, or null. Remembers the version opened (edit clash).</summary>
    public async Task<StockChangeEntity?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var found = await db.StockChanges
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (found is null || TypeOf(found) is null)
        {
            return null;
        }

        db.MarkSeen(found);
        return found;
    }

    /// <param name="placeId">Stage 3, batch 3-4: the place the stock is counted, found damaged or written off in (null = where sales leave from).</param>
    public async Task<StockChangeEntity> CreateAsync(StockEntryType type, DateOnly date, string? reason, string? remarks, int? placeId = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var kind = KindOf(type);
        if (placeId is { } chosen && !await db.Places.AnyAsync(p => p.Id == chosen))
        {
            throw new StockChangeRefused("Choose the place first.");
        }

        var entry = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Date = date,
            VoucherId = await StockChangeService.NextVoucherAsync(db, kind, date),
            Status = await StockChangeService.NewStatusAsync(db),
            Reason = Clean(reason),
            Remarks = Clean(remarks),
            PlaceId = placeId ?? await PlaceSettings.SalesPlaceAsync(db),
        };
        db.StockChanges.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    /// <summary>Saves date, reason (also on every line) and remarks. The type and the voucher number stay.</summary>
    public async Task UpdateAsync(Guid id, DateOnly date, string? reason, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id);
        if (entry is null)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(id);
            throw new StockChangeRefused("This stock change was not found.");
        }

        entry.Date = date;
        entry.Reason = Clean(reason);
        entry.Remarks = Clean(remarks);
        foreach (var line in entry.Lines)
        {
            line.Reason = entry.Reason;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Deletes an entry; refused when damage on it was already repaired, written off or sent back.</summary>
    public async Task DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id);
        if (entry is null)
        {
            return;
        }

        foreach (var line in entry.Lines)
        {
            if (await DealtWithAsync(db, line.Id) > 0)
            {
                throw new StockChangeRefused("Some of this damage was already repaired, written off or sent back, so the entry cannot be deleted.");
            }
        }

        db.StockChanges.Remove(entry);
        await db.SaveChangesAsync();
    }

    public async Task AddLinesAsync(Guid id, IReadOnlyList<(Guid ProductId, Guid VariantId, int? Quantity, int SerialNumber, string? Note)> lines)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id);
        if (entry is null || TypeOf(entry) is not { } type)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(id);
            throw new StockChangeRefused("This stock change was not found.");
        }

        foreach (var l in lines)
        {
            if (l.ProductId == Guid.Empty || l.VariantId == Guid.Empty)
            {
                throw new StockChangeRefused("Select a product and variant before adding a line.");
            }

            if (QuantityProblem(type, l.Quantity) is { } problem)
            {
                throw new StockChangeRefused(problem);
            }
        }

        await StockChangeService.NeedsApprovalAgainAsync(db, entry);
        foreach (var l in lines)
        {
            var (from, to) = MoveOf(type, l.Quantity!.Value);
            db.StockChangeLines.Add(new StockChangeLineEntity
            {
                Id = Guid.NewGuid(),
                StockChangeEntityId = id,
                SerialNumber = l.SerialNumber,
                ProductEntityId = l.ProductId,
                VariantEntityId = l.VariantId,
                Quantity = Math.Abs(l.Quantity.Value),
                From = from,
                To = to,
                Reason = entry.Reason,
                Note = Clean(l.Note),
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task UpdateLineAsync(Guid lineId, Guid productId, Guid variantId, int? quantity, int serialNumber, string? note)
    {
        await using var db = await factory.CreateDbContextAsync();
        var line = await db.StockChangeLines.Include(l => l.StockChange).ThenInclude(s => s!.Lines).FirstOrDefaultAsync(l => l.Id == lineId);
        if (line?.StockChange is null || TypeOf(line.StockChange) is not { } type)
        {
            db.ThrowPartGone<StockChangeLineEntity>();
            return;
        }

        if (QuantityProblem(type, quantity) is { } problem)
        {
            throw new StockChangeRefused(problem);
        }

        var dealt = await DealtWithAsync(db, lineId);
        if (dealt > 0 && (line.ProductEntityId != productId || line.VariantEntityId != variantId))
        {
            throw new StockChangeRefused($"{Pieces(dealt)} of this damage {(dealt == 1 ? "was" : "were")} already repaired, written off or sent back, so the line cannot be changed to another item.");
        }

        if (Math.Abs(quantity!.Value) < dealt)
        {
            throw new StockChangeRefused($"{Pieces(dealt)} of this damage {(dealt == 1 ? "was" : "were")} already repaired, written off or sent back, so the quantity cannot be less than {dealt}.");
        }

        var (from, to) = MoveOf(type, quantity.Value);
        if (line.ProductEntityId != productId || line.VariantEntityId != variantId || line.Quantity != Math.Abs(quantity.Value) || line.From != from)
        {
            await StockChangeService.NeedsApprovalAgainAsync(db, line.StockChange);
        }

        line.ProductEntityId = productId;
        line.VariantEntityId = variantId;
        line.Quantity = Math.Abs(quantity.Value);
        line.From = from;
        line.To = to;
        line.SerialNumber = serialNumber;
        line.Note = Clean(note);
        await db.SaveChangesAsync();
    }

    public async Task DeleteLineAsync(Guid lineId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var line = await db.StockChangeLines.Include(l => l.StockChange).FirstOrDefaultAsync(l => l.Id == lineId);
        if (line is null)
        {
            return;
        }

        if (await DealtWithAsync(db, lineId) > 0)
        {
            throw new StockChangeRefused("Some of this damage was already repaired, written off or sent back, so the line cannot be deleted.");
        }

        await StockChangeService.NeedsApprovalAgainAsync(db, line.StockChange!);
        db.StockChangeLines.Remove(line);
        await db.SaveChangesAsync();
    }

    /// <summary>Pieces of a damage line that later stock changes took (repaired, written off, sent back; approved or waiting).</summary>
    private static async Task<int> DealtWithAsync(AppDbContext db, Guid damageLineId) =>
        await db.StockChangeLines.AsNoTracking()
            .Where(t => t.SourceLineId == damageLineId && t.StockChange!.Kind != StockChangeKind.Damage && t.StockChange.Status != StockChangeStatus.Rejected)
            .SumAsync(t => (int?)t.Quantity) ?? 0;

    private static string Pieces(int n) => n == 1 ? "1 piece" : $"{n} pieces";

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // ---------------------------------------------------------------- the reasons list (batch 2-7)

    public async Task<List<ReasonItem>> AllReasonsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.StockChangeReasons.AsNoTracking().OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new ReasonItem(r.Id, r.Name, r.IsActive, r.SortOrder)).ToListAsync();
    }

    public async Task AddReasonAsync(string? name)
    {
        var clean = Clean(name) ?? throw new StockChangeRefused("Type the reason first.");
        await using var db = await factory.CreateDbContextAsync();
        if (await db.StockChangeReasons.AnyAsync(r => r.Name.ToLower() == clean.ToLower()))
        {
            throw new StockChangeRefused($"\"{clean}\" is already on the list.");
        }

        var last = await db.StockChangeReasons.MaxAsync(r => (int?)r.SortOrder) ?? 0;
        db.StockChangeReasons.Add(new StockChangeReasonEntity { Name = clean, SortOrder = last + 1, IsActive = true });
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Stock change reason added: {clean}");
    }

    /// <summary>Renames a reason for future use; entries made earlier keep the words they were saved with.</summary>
    public async Task RenameReasonAsync(int id, string? name)
    {
        var clean = Clean(name) ?? throw new StockChangeRefused("A reason cannot be empty.");
        await using var db = await factory.CreateDbContextAsync();
        var reason = await db.StockChangeReasons.FindAsync(id) ?? throw new StockChangeRefused("This reason is no longer there.");
        if (reason.Name == clean)
        {
            return;
        }

        if (await db.StockChangeReasons.AnyAsync(r => r.Id != id && r.Name.ToLower() == clean.ToLower()))
        {
            throw new StockChangeRefused($"\"{clean}\" is already on the list.");
        }

        var old = reason.Name;
        reason.Name = clean;
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Stock change reason renamed: {old} → {clean}");
    }

    /// <summary>Takes a reason off the list (or puts it back). It is never deleted, so nothing that used it changes.</summary>
    public async Task SetReasonActiveAsync(int id, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var reason = await db.StockChangeReasons.FindAsync(id) ?? throw new StockChangeRefused("This reason is no longer there.");
        if (!active && await db.StockChangeReasons.CountAsync(r => r.IsActive) <= 1)
        {
            throw new StockChangeRefused("At least one reason must stay on the list.");
        }

        reason.IsActive = active;
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Stock change reason {(active ? "put back on the list" : "taken off the list")}: {reason.Name}");
    }

    /// <summary>Moves a reason one place up (-1) or down (+1) in the list.</summary>
    public async Task MoveReasonAsync(int id, int step)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.StockChangeReasons.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync();
        var index = list.FindIndex(r => r.Id == id);
        var other = index + Math.Sign(step);
        if (index < 0 || other < 0 || other >= list.Count)
        {
            return;
        }

        (list[index], list[other]) = (list[other], list[index]);
        for (var i = 0; i < list.Count; i++)
        {
            list[i].SortOrder = i + 1;
        }

        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- approval (batch 2-8)

    public async Task<bool> IsApprovalOnAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await StockChangeService.ApprovalOnAsync(db);
    }

    /// <summary>
    /// The Admin's switch (Hiten, 25-26 Nov 2026): with it on, every stock change waits for approval before it moves stock.
    /// Only someone holding the Admin role may change it; the change is written to the activity log.
    /// </summary>
    public async Task SetApprovalAsync(bool on, UserAccess access)
    {
        if (!access.HasAdminRole)
        {
            throw new StockChangeRefused("Only the Admin can switch approval on or off.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var setting = await db.AppSettings.FindAsync(StockChangeService.ApprovalSetting);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSettingEntity { Key = StockChangeService.ApprovalSetting, Value = on ? "on" : "off" });
        }
        else
        {
            setting.Value = on ? "on" : "off";
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", on ? "Stock changes need approval: switched on" : "Stock changes need approval: switched off", access.Name);
    }

    public async Task<int> WaitingCountAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.StockChanges.CountAsync(s => s.Status == StockChangeStatus.Waiting);
    }

    /// <summary>Everything waiting for approval, oldest first, each line in plain words ("P1 Product / V1: 2 damaged").</summary>
    public async Task<List<WaitingEntry>> WaitingAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var entries = await db.StockChanges.AsNoTracking().AsSplitQuery()
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .Include(s => s.Customer).Include(s => s.Supplier)
            .Where(s => s.Status == StockChangeStatus.Waiting)
            .ToListAsync();
        var purchaseIds = entries.Where(e => e.PurchaseEntityId is not null).Select(e => e.PurchaseEntityId!.Value).ToList();
        var purchases = await db.Purchases.AsNoTracking().Where(p => purchaseIds.Contains(p.Id))
            .Select(p => new { p.Id, p.VoucherId, p.Date, Supplier = p.Supplier!.Name }).ToDictionaryAsync(p => p.Id);

        return entries.OrderBy(e => e.ChangedAt ?? e.CreatedAt).ThenBy(e => e.VoucherId).Select(e =>
        {
            string? party = e.Customer?.Name ?? e.Supplier?.Name;
            string? link = e.Kind switch
            {
                StockChangeKind.SalesReturn => $"/salesreturn/{e.Id}",
                StockChangeKind.PurchaseReturn => $"/purchasereturn/{e.Id}",
                _ when MoveService.IsMove(e.Kind) => $"/transfer/{e.Id}",   // stage 3, batch 3-5
                StockChangeKind.WriteOff when e.SourceStockChangeId is { } m => $"/transfer/{m}",   // missing on a move (3-6)
                _ when TypeOf(e) is not null => $"/stockchange/{e.Id}",
                StockChangeKind.Damage when e.SourceStockChangeId is { } r => $"/salesreturn/{r}",
                _ => "/damage",
            };
            if (e.PurchaseEntityId is { } p && purchases.TryGetValue(p, out var purchase) && !MoveService.IsMove(e.Kind))
            {
                party = $"Purchase {purchase.VoucherId} of {purchase.Date:dd-MM-yyyy}, {purchase.Supplier}";
                link = $"/purchase/{p}";
            }

            var lines = e.Lines.OrderBy(l => l.SerialNumber)
                .Select(l => $"{l.Product?.ProductId} {l.Product?.Name} / {l.ProductVariant?.VariantId}: {l.Quantity} {Move(l)}".Trim())
                .ToList();
            return new WaitingEntry(e.Id, ActivityLogWriter.StockChangeLabel(e.Kind, e.VoucherId, e.Date), party,
                e.ChangedAt ?? e.CreatedAt, e.ChangedBy ?? e.CreatedBy, lines, link, e.Remarks);
        }).ToList();
    }

    /// <summary>"damaged", "back to sellable", "into stock", "out of stock", "out of damage".</summary>
    public static string Move(StockChangeLineEntity line) => (line.From, line.To) switch
    {
        (StockState.Sellable, StockState.Damaged) => "damaged",
        (StockState.Damaged, StockState.Sellable) => "back to sellable",
        (StockState.Outside, StockState.Sellable) => "into stock",
        (StockState.Outside, StockState.Damaged) => "into damage",
        (StockState.Sellable, StockState.Outside) => "out of stock",
        (StockState.Damaged, StockState.Outside) => "out of damage",
        (StockState.Sellable, StockState.InTransit) => "sent, on the way",          // stage 3, batches 3-5/3-6
        (StockState.InTransit, StockState.Sellable) => "received",
        (StockState.Sellable, StockState.Sellable) => "moved",
        (StockState.InTransit, StockState.Outside) => "written off (missing on the way)",
        _ => $"{line.From} → {line.To}",
    };

    /// <summary>Approves (stock moves now) or rejects (stock never moves) a waiting stock change. Needs the switch "Approve stock changes".</summary>
    public async Task DecideAsync(Guid id, bool approve, UserAccess access)
    {
        if (!access.Allows(Permissions.StockApprove))
        {
            throw new StockChangeRefused("You may not approve stock changes.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.StockChanges.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new StockChangeRefused("This stock change is no longer there.");
        if (entry.Status != StockChangeStatus.Waiting)
        {
            throw new StockChangeRefused($"This stock change was already {(entry.Status == StockChangeStatus.Approved ? "approved" : "rejected")}{(entry.DecidedBy is { } by ? $" by {by}" : "")}.");
        }

        entry.Status = approve ? StockChangeStatus.Approved : StockChangeStatus.Rejected;
        entry.DecidedBy = access.Name;
        entry.DecidedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }
}
