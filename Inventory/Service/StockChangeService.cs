using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>Stage 2: where damage is being recorded.</summary>
public enum DamageSource
{
    Purchase,
    SalesReturn,
}

/// <summary>
/// One line of the "Record damage" pop-up: a line of the purchase or sales return, how many of its pieces are damaged now,
/// and how many of those have already been dealt with (repaired, written off, sent back), which cannot be undone here.
/// </summary>
public sealed record DamageCandidate(
    Guid LineId, int SerialNumber, Guid ProductEntityId, string ProductId, string ProductName,
    Guid VariantEntityId, string VariantId, string? VariantName, int Received, int Damaged, int DealtWith = 0)
{
    /// <summary>The fewest damaged pieces this line can be set to.</summary>
    public int Lowest => DealtWith;

    /// <summary>"AJ0120103 1 NO HAATICHAP(NS-11) / G".</summary>
    public string Label => $"{ProductId} {ProductName}".Trim() + $" / {VariantId}";
}

/// <summary>
/// What the person typed for one line: how many pieces of it are damaged now (null = unchanged), and the reason for
/// newly damaged pieces (Hiten, 26 Nov 2026: recorded damage can be changed, up or down).
/// </summary>
public sealed record DamageEntry(Guid LineId, int? Damaged, string? Reason);

/// <summary>What one save of the pop-up did: pieces newly damaged, and pieces found not damaged after all.</summary>
public sealed record DamageResult(int MoreDamaged, int NotDamaged)
{
    public bool Changed => MoreDamaged > 0 || NotDamaged > 0;

    /// <summary>"Damage recorded: 2 more damaged, 1 not damaged after all."</summary>
    public string Text => "Damage recorded: " + string.Join(", ", new[]
    {
        MoreDamaged > 0 ? $"{MoreDamaged} more damaged" : null,
        NotDamaged > 0 ? $"{NotDamaged} not damaged after all" : null,
    }.Where(t => t is not null)) + ".";
}

/// <summary>One row of the Sales Returns list: pieces that came back, and pieces given in exchange.</summary>
public sealed record SalesReturnListItem(Guid Id, int VoucherId, DateOnly Date, string CustomerName, int Pieces, string? Remarks, int Given = 0);

/// <summary>
/// Version 3.0, stage 2, batches 2-3 and 2-4: the rule book for stock changes – recording (and correcting) damage on a
/// purchase or a sales return, and the sales return itself with goods given in exchange. Every screen goes through here
/// (never its own copy of the rules). Each save is one database save, so the activity log, stamps and "someone else
/// changed this" checks apply as for every other record.
/// </summary>
public class StockChangeService(IDbContextFactory<AppDbContext> factory)
{
    /// <summary>
    /// Stage 2 (Hiten, 25 Nov 2026): approval is built but switched off, so a stock change counts at once.
    /// Batch 2-8 adds the switch; with it on, new stock changes wait for approval.
    /// </summary>
    internal static StockChangeStatus NewStatus() => StockChangeStatus.Approved;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---------------------------------------------------------------- damage (batch 2-3)

    /// <summary>The lines of a purchase or sales return (pieces that came in), with how many of each are damaged now.</summary>
    public async Task<List<DamageCandidate>> DamageCandidatesAsync(DamageSource source, Guid sourceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await CandidatesAsync(db, source, sourceId);
    }

    private static async Task<List<DamageCandidate>> CandidatesAsync(AppDbContext db, DamageSource source, Guid sourceId)
    {
        if (source == DamageSource.Purchase)
        {
            var lines = await db.PurchaseVariant.AsNoTracking()
                .Where(l => l.PurchaseEntityId == sourceId)
                .Select(l => new { l.Id, l.SerialNumber, l.ProductEntityId, l.Product!.ProductId, ProductName = l.Product.Name, l.VariantEntityId, l.ProductVariant!.VariantId, VariantName = l.ProductVariant.Name, Qty = l.Quantity ?? 0 })
                .ToListAsync();
            var ids = lines.Select(l => l.Id).ToList();
            var damage = await DamageLinesAsync(db, ids, purchase: true);
            return lines.OrderBy(l => l.SerialNumber)
                .Select(l => new DamageCandidate(l.Id, l.SerialNumber, l.ProductEntityId, l.ProductId ?? "", l.ProductName ?? "", l.VariantEntityId, l.VariantId ?? "", l.VariantName, l.Qty,
                    damage.Damaged.GetValueOrDefault(l.Id), damage.DealtWith.GetValueOrDefault(l.Id)))
                .ToList();
        }

        // A sales return: only the pieces that came back (its "given in exchange" lines went out).
        var returned = await db.StockChangeLines.AsNoTracking()
            .Where(l => l.StockChangeEntityId == sourceId && l.StockChange!.Kind == StockChangeKind.SalesReturn && l.From == StockState.Outside)
            .Select(l => new { l.Id, l.SerialNumber, l.ProductEntityId, l.Product!.ProductId, ProductName = l.Product.Name, l.VariantEntityId, l.ProductVariant!.VariantId, VariantName = l.ProductVariant.Name, l.Quantity })
            .ToListAsync();
        var returnIds = returned.Select(l => l.Id).ToList();
        var onReturn = await DamageLinesAsync(db, returnIds, purchase: false);
        return returned.OrderBy(l => l.SerialNumber)
            .Select(l => new DamageCandidate(l.Id, l.SerialNumber, l.ProductEntityId, l.ProductId ?? "", l.ProductName ?? "", l.VariantEntityId, l.VariantId ?? "", l.VariantName, l.Quantity,
                onReturn.Damaged.GetValueOrDefault(l.Id), onReturn.DealtWith.GetValueOrDefault(l.Id)))
            .ToList();
    }

    /// <summary>
    /// Per source line (purchase line or returned line): pieces damaged now (damage lines in, minus "not damaged after all"
    /// lines out; approved or waiting), and pieces of that damage already dealt with by later stock changes (repaired,
    /// written off, sent back – batches 2-5/2-6), which cannot be undone from the pop-up.
    /// </summary>
    private static async Task<(Dictionary<Guid, int> Damaged, Dictionary<Guid, int> DealtWith)> DamageLinesAsync(AppDbContext db, List<Guid> sourceLineIds, bool purchase)
    {
        var query = db.StockChangeLines.AsNoTracking()
            .Where(d => d.StockChange!.Kind == StockChangeKind.Damage && d.StockChange.Status != StockChangeStatus.Rejected);
        query = purchase
            ? query.Where(d => d.PurchaseVariantId != null && sourceLineIds.Contains(d.PurchaseVariantId.Value))
            : query.Where(d => d.SourceLineId != null && sourceLineIds.Contains(d.SourceLineId.Value));
        var damageLines = (await query.Select(d => new { d.Id, d.PurchaseVariantId, d.SourceLineId, d.To, d.Quantity }).ToListAsync())
            .Select(d => (d.Id, Owner: (purchase ? d.PurchaseVariantId : d.SourceLineId)!.Value, Signed: d.To == StockState.Damaged ? d.Quantity : -d.Quantity))
            .ToList();
        var damaged = damageLines.GroupBy(d => d.Owner).ToDictionary(g => g.Key, g => g.Sum(d => d.Signed));

        var dealt = new Dictionary<Guid, int>();
        if (damageLines.Count > 0)
        {
            var damageIds = damageLines.Select(d => d.Id).ToList();
            var taken = await db.StockChangeLines.AsNoTracking()
                .Where(t => t.SourceLineId != null && damageIds.Contains(t.SourceLineId.Value)
                            && t.StockChange!.Kind != StockChangeKind.Damage && t.StockChange.Status != StockChangeStatus.Rejected)
                .Select(t => new { Damage = t.SourceLineId!.Value, t.Quantity })
                .ToListAsync();
            foreach (var t in taken)
            {
                var owner = damageLines.First(d => d.Id == t.Damage).Owner;
                dealt[owner] = dealt.GetValueOrDefault(owner) + t.Quantity;
            }
        }

        return (damaged, dealt);
    }

    /// <summary>The reasons offered, in their order (taken-off ones left out).</summary>
    public async Task<List<string>> ReasonsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.StockChangeReasons.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.SortOrder).ThenBy(r => r.Name).Select(r => r.Name).ToListAsync();
    }

    /// <summary>
    /// Saves the damaged pieces typed in the pop-up as one stock change, linked to the purchase or sales return and its lines:
    /// a line set higher moves the extra pieces Sellable → Damaged with its reason; a line set lower moves pieces back
    /// Damaged → Sellable, "Not damaged after all". Nothing earlier is changed or removed, so the history stays complete.
    /// Checked again against the database at the moment of saving. Throws <see cref="StockChangeRefused"/> with a plain message.
    /// </summary>
    public async Task<DamageResult> RecordDamageAsync(DamageSource source, Guid sourceId, IReadOnlyList<DamageEntry> entries, string? note)
    {
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var candidates = await CandidatesAsync(db, source, sourceId);
        var problem = StockChangeRules.DamageProblem(candidates, entries);
        if (problem is not null)
        {
            throw new StockChangeRefused(problem);
        }

        Guid? supplier = null;
        if (source == DamageSource.Purchase)
        {
            supplier = await db.Purchases.Where(p => p.Id == sourceId).Select(p => (Guid?)p.SupplierEntityId).FirstOrDefaultAsync()
                ?? throw new StockChangeRefused("This purchase was not found. It may have been deleted by someone else.");
        }
        else if (!await db.StockChanges.AnyAsync(s => s.Id == sourceId && s.Kind == StockChangeKind.SalesReturn))
        {
            throw new StockChangeRefused("This sales return was not found. It may have been deleted by someone else.");
        }

        var today = Today;
        var change = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = StockChangeKind.Damage,
            Date = today,
            VoucherId = await NextVoucherAsync(db, StockChangeKind.Damage, today),
            Status = NewStatus(),
            PurchaseEntityId = source == DamageSource.Purchase ? sourceId : null,
            SourceStockChangeId = source == DamageSource.SalesReturn ? sourceId : null,
            Remarks = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        var serial = 1;
        var more = 0;
        var back = 0;
        foreach (var entry in entries)
        {
            var line = candidates.First(c => c.LineId == entry.LineId);
            var difference = (entry.Damaged ?? line.Damaged) - line.Damaged;
            if (difference == 0)
            {
                continue;
            }

            var up = difference > 0;
            change.Lines.Add(new StockChangeLineEntity
            {
                Id = Guid.NewGuid(),
                SerialNumber = serial++,
                ProductEntityId = line.ProductEntityId,
                VariantEntityId = line.VariantEntityId,
                Quantity = Math.Abs(difference),
                From = up ? StockState.Sellable : StockState.Damaged,
                To = up ? StockState.Damaged : StockState.Sellable,
                Reason = up ? (string.IsNullOrWhiteSpace(entry.Reason) ? null : entry.Reason.Trim()) : StockReasons.NotDamaged,
                PurchaseVariantId = source == DamageSource.Purchase ? line.LineId : null,
                SourceLineId = source == DamageSource.SalesReturn ? line.LineId : null,
                ReturnSupplierEntityId = up ? supplier : null,   // Hiten: damage from a purchase goes back to that purchase's supplier unless changed
            });
            if (up)
            {
                more += difference;
            }
            else
            {
                back -= difference;
            }
        }

        db.StockChanges.Add(change);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new DamageResult(more, back);
    }

    internal static async Task<int> NextVoucherAsync(AppDbContext db, StockChangeKind kind, DateOnly date) =>
        (await db.StockChanges.Where(s => s.Kind == kind && s.Date == date).MaxAsync(s => (int?)s.VoucherId) ?? 0) + 1;

    // ---------------------------------------------------------------- sales returns (batch 2-4)

    /// <summary>The Sales Returns list, newest first.</summary>
    public async Task<List<SalesReturnListItem>> SalesReturnsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.StockChanges.AsNoTracking()
            .Where(s => s.Kind == StockChangeKind.SalesReturn)
            .Select(s => new
            {
                s.Id, s.VoucherId, s.Date, Customer = s.Customer!.Name, s.Remarks,
                Pieces = s.Lines.Where(l => l.From == StockState.Outside).Sum(l => (int?)l.Quantity) ?? 0,
                Given = s.Lines.Where(l => l.To == StockState.Outside).Sum(l => (int?)l.Quantity) ?? 0,
            })
            .ToListAsync();
        return rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.VoucherId)
            .Select(r => new SalesReturnListItem(r.Id, r.VoucherId, r.Date, r.Customer ?? "", r.Pieces, r.Remarks, r.Given)).ToList();
    }

    /// <summary>A sales return with its customer and lines, or null when it does not exist. Remembers the version opened (edit clash).</summary>
    public async Task<StockChangeEntity?> GetSalesReturnAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var found = await db.StockChanges
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(s => s.Customer)
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .FirstOrDefaultAsync(s => s.Id == id && s.Kind == StockChangeKind.SalesReturn);
        db.MarkSeen(found);
        return found;
    }

    public async Task<StockChangeEntity> CreateSalesReturnAsync(Guid customerId, DateOnly date, string? remarks)
    {
        if (customerId == Guid.Empty)
        {
            throw new StockChangeRefused("Please select a customer.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var change = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = StockChangeKind.SalesReturn,
            Date = date,
            VoucherId = await NextVoucherAsync(db, StockChangeKind.SalesReturn, date),
            Status = NewStatus(),
            CustomerEntityId = customerId,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
        };
        db.StockChanges.Add(change);
        await db.SaveChangesAsync();   // the save marks it "being created", so lines added on this screen fold into its "Created" entry
        return change;
    }

    /// <summary>Saves customer, date and remarks. The voucher number stays.</summary>
    public async Task UpdateSalesReturnAsync(Guid id, Guid customerId, DateOnly date, string? remarks)
    {
        if (customerId == Guid.Empty)
        {
            throw new StockChangeRefused("Please select a customer.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StockChanges.FirstOrDefaultAsync(s => s.Id == id && s.Kind == StockChangeKind.SalesReturn);
        if (existing is null)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(id);
            throw new StockChangeRefused("This sales return was not found.");
        }

        existing.CustomerEntityId = customerId;
        existing.Date = date;
        existing.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        await db.SaveChangesAsync();
    }

    /// <summary>Deletes a sales return with its lines (returned and given in exchange); refused when damage was recorded on it.</summary>
    public async Task DeleteSalesReturnAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.StockChanges.AnyAsync(s => s.SourceStockChangeId == id))
        {
            throw new StockChangeRefused("Damage is recorded on this sales return, so it cannot be deleted.");
        }

        var existing = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id && s.Kind == StockChangeKind.SalesReturn);
        if (existing is null)
        {
            return;
        }

        db.StockChanges.Remove(existing);
        await db.SaveChangesAsync();
    }

    /// <summary>Adds the pieces a customer brought back: Outside → Sellable.</summary>
    public async Task AddSalesReturnLineAsync(Guid returnId, Guid productId, Guid variantId, int? quantity, int serialNumber) =>
        await AddSalesReturnLinesAsync(returnId, [(productId, variantId, quantity, serialNumber)]);

    /// <summary>Adds several returned lines in one save (all or nothing).</summary>
    public Task AddSalesReturnLinesAsync(Guid returnId, IReadOnlyList<(Guid ProductId, Guid VariantId, int? Quantity, int SerialNumber)> lines) =>
        AddLinesAsync(returnId, lines, StockState.Outside, StockState.Sellable, "The quantity returned must be more than 0.");

    /// <summary>
    /// Hiten, 26 Nov 2026: goods given to the customer in exchange, on the same sales return: Sellable → Outside. Numbered on
    /// after the return's lines. All or nothing.
    /// </summary>
    public async Task AddExchangeLinesAsync(Guid returnId, IReadOnlyList<(Guid ProductId, Guid VariantId, int? Quantity)> lines)
    {
        await using var db = await factory.CreateDbContextAsync();
        var first = (await db.StockChangeLines.Where(l => l.StockChangeEntityId == returnId).MaxAsync(l => (int?)l.SerialNumber) ?? 0) + 1;
        await AddLinesAsync(returnId, lines.Select((l, i) => (l.ProductId, l.VariantId, l.Quantity, first + i)).ToList(),
            StockState.Sellable, StockState.Outside, "The quantity given in exchange must be more than 0.");
    }

    private async Task AddLinesAsync(Guid returnId, IReadOnlyList<(Guid ProductId, Guid VariantId, int? Quantity, int SerialNumber)> lines, StockState from, StockState to, string positive)
    {
        if (lines.Count == 0)
        {
            return;
        }

        foreach (var l in lines)
        {
            if (StockChangeRules.ReturnLineProblem(l.ProductId, l.VariantId, l.Quantity, positive) is { } problem)
            {
                throw new StockChangeRefused(problem);
            }
        }

        await using var db = await factory.CreateDbContextAsync();
        var header = await db.StockChanges.FirstOrDefaultAsync(s => s.Id == returnId && s.Kind == StockChangeKind.SalesReturn);
        if (header is null)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(returnId);
            throw new StockChangeRefused("This sales return was not found.");
        }

        foreach (var l in lines)
        {
            db.StockChangeLines.Add(new StockChangeLineEntity
            {
                Id = Guid.NewGuid(),
                StockChangeEntityId = returnId,
                SerialNumber = l.SerialNumber,
                ProductEntityId = l.ProductId,
                VariantEntityId = l.VariantId,
                Quantity = l.Quantity!.Value,
                From = from,
                To = to,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Changes a returned line. Refused when it would go below the pieces damaged on it, or change item.</summary>
    public async Task UpdateSalesReturnLineAsync(Guid lineId, Guid productId, Guid variantId, int? quantity, int serialNumber)
    {
        if (StockChangeRules.ReturnLineProblem(productId, variantId, quantity) is { } problem)
        {
            throw new StockChangeRefused(problem);
        }

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StockChangeLines.FirstOrDefaultAsync(l => l.Id == lineId);
        if (existing is null)
        {
            db.ThrowPartGone<StockChangeLineEntity>();
            return;
        }

        var damaged = await DamagedOnLineAsync(db, lineId, purchaseLine: false);
        if (StockChangeRules.LineChangeProblem(damaged, existing.ProductEntityId, existing.VariantEntityId, productId, variantId, quantity) is { } refused)
        {
            throw new StockChangeRefused(refused);
        }

        existing.ProductEntityId = productId;
        existing.VariantEntityId = variantId;
        existing.Quantity = quantity!.Value;
        existing.SerialNumber = serialNumber;
        await db.SaveChangesAsync();
    }

    /// <summary>Deletes a returned line or a line given in exchange. A returned line with damage on it is refused.</summary>
    public async Task DeleteSalesReturnLineAsync(Guid lineId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StockChangeLines.FirstOrDefaultAsync(l => l.Id == lineId);
        if (existing is null)
        {
            return;
        }

        if (await DamagedOnLineAsync(db, lineId, purchaseLine: false) > 0)
        {
            throw new StockChangeRefused(StockChangeRules.DamagedLineCannotBeDeleted);
        }

        db.StockChangeLines.Remove(existing);
        await db.SaveChangesAsync();
    }

    /// <summary>Pieces damaged now on a purchase line or a sales return line (approved or waiting; corrections taken off).</summary>
    public static async Task<int> DamagedOnLineAsync(AppDbContext db, Guid lineId, bool purchaseLine)
    {
        var lines = db.StockChangeLines.AsNoTracking()
            .Where(d => d.StockChange!.Kind == StockChangeKind.Damage && d.StockChange.Status != StockChangeStatus.Rejected);
        lines = purchaseLine ? lines.Where(d => d.PurchaseVariantId == lineId) : lines.Where(d => d.SourceLineId == lineId);
        return await lines.SumAsync(d => (int?)(d.To == StockState.Damaged ? d.Quantity : -d.Quantity)) ?? 0;
    }
}

/// <summary>A stock change refused by the rules, with a message for the operator.</summary>
public sealed class StockChangeRefused(string message) : Exception(message);

/// <summary>Stage 2: the rules for stock changes, as plain functions (one place, tested on their own).</summary>
public static class StockChangeRules
{
    public const string DamagedLineCannotBeDeleted = "Damage is recorded on this line, so it cannot be deleted.";
    public const string DamagedPurchaseCannotBeDeleted = "Damage is recorded on this purchase, so it cannot be deleted.";
    public const string NothingChanged = "Nothing to save: change the damaged pieces on at least one line.";

    /// <summary>What is wrong with the damaged pieces typed in the pop-up, or null when they can be saved.</summary>
    public static string? DamageProblem(IReadOnlyList<DamageCandidate> candidates, IReadOnlyList<DamageEntry> entries)
    {
        var any = false;
        foreach (var entry in entries)
        {
            var line = candidates.FirstOrDefault(c => c.LineId == entry.LineId);
            if (line is null)
            {
                return "A line of this entry is no longer there. Close the window and open it again.";
            }

            if (entry.Damaged is not { } wanted || wanted == line.Damaged)
            {
                continue;
            }

            if (wanted < 0)
            {
                return $"{line.Label}: the damaged pieces must be 0 or more.";
            }

            if (wanted > line.Received)
            {
                return $"{line.Label}: only {line.Received} piece{(line.Received == 1 ? "" : "s")} came in, so no more than {line.Received} can be damaged.";
            }

            if (wanted < line.Lowest)
            {
                return $"{line.Label}: {line.Lowest} damaged piece{(line.Lowest == 1 ? " has" : "s have")} already been repaired, written off or sent back, so it cannot be less than {line.Lowest}.";
            }

            any = true;
        }

        return any ? null : NothingChanged;
    }

    /// <summary>A sales return line (returned or given in exchange): a real product and variant, and more than 0 pieces.</summary>
    public static string? ReturnLineProblem(Guid productId, Guid? variantId, int? quantity, string positive = "The quantity returned must be more than 0.")
    {
        if (productId == Guid.Empty || variantId is null || variantId == Guid.Empty)
        {
            return "Select a product and variant before adding a line.";
        }

        return quantity is null or <= 0 ? positive : null;
    }

    /// <summary>
    /// A purchase or sales return line on which damage is recorded may not drop below the damaged pieces or become another
    /// item (the damage would no longer match what came in). Null when the change is fine.
    /// </summary>
    public static string? LineChangeProblem(int damaged, Guid oldProduct, Guid oldVariant, Guid newProduct, Guid? newVariant, int? newQuantity)
    {
        if (damaged <= 0)
        {
            return null;
        }

        var pieces = damaged == 1 ? "1 piece of this line is" : $"{damaged} pieces of this line are";
        if (oldProduct != newProduct || oldVariant != newVariant)
        {
            return $"{pieces} recorded as damaged, so the line cannot be changed to another item.";
        }

        return newQuantity < damaged ? $"{pieces} recorded as damaged, so the quantity cannot be less than {damaged}." : null;
    }
}
