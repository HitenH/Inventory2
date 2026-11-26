using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>
/// One row of the damage report: the damaged pieces of one purchase line or one returned line (or, from batch 2-7, one damage
/// entry made in the shop) that are still lying in damage.
/// </summary>
public sealed record DamagedLot(
    Guid Key, Guid ProductEntityId, string ProductId, string ProductName, Guid VariantEntityId, string VariantId, string? VariantName,
    int Pieces, DateOnly Since, string From, string? FromLink, string Reasons, Guid? SupplierId, string? SupplierName, bool ReadyToSend,
    int Waiting = 0)
{
    // Pieces: lying in damage and free to act on. Waiting (batch 2-8): pieces of this row on stock changes waiting for approval
    // (damage recorded or lowered, repairs, write-offs, sending back) - they move only when approved.

    /// <summary>"Ready to send", "Supplier assigned" or "No supplier yet".</summary>
    public string Status => ReadyToSend ? "Ready to send" : SupplierId is null ? "No supplier yet" : "Supplier assigned";
}

/// <summary>How many pieces of a damage report row an action takes (the whole row unless the person typed fewer).</summary>
public sealed record LotPieces(Guid Key, int Pieces);

/// <summary>One row of the Purchase Returns list.</summary>
public sealed record PurchaseReturnListItem(Guid Id, int VoucherId, DateOnly Date, string SupplierName, int FromDamage, int GoodStock, string? Remarks);

/// <summary>
/// Version 3.0, stage 2, batches 2-5 and 2-6 (Hiten, 25 Nov 2026): the damage report – everything lying in damage, with
/// the supplier it goes back to and "ready to send"; Repaired (back to sellable), Written off, and Sent, which makes a
/// Purchase Return voucher – and the purchase returns themselves (goods sent back to a supplier, from damage or good stock).
/// After a purchase return nothing more is tracked: what comes back later is a new purchase (Hiten).
/// Every change is one database save, with stamps, the activity log and the "someone else changed this" check.
/// </summary>
public class DamagedGoodsService(IDbContextFactory<AppDbContext> factory)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // ---------------------------------------------------------------- the damage report (batch 2-5)

    /// <summary>Everything lying in damage now, one row per purchase line / returned line, oldest first.</summary>
    public async Task<List<DamagedLot>> LotsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var groups = await GroupsAsync(db);
        if (groups.Count == 0)
        {
            return [];
        }

        var productIds = groups.Select(g => g.Product).Distinct().ToList();
        var variantIds = groups.Select(g => g.Variant).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var variants = await db.Variants.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        var supplierIds = groups.Where(g => g.Supplier is not null).Select(g => g.Supplier!.Value).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);

        var purchaseIds = groups.Where(g => g.Purchase is not null).Select(g => g.Purchase!.Value).Distinct().ToList();
        var purchases = await db.Purchases.AsNoTracking().Where(p => purchaseIds.Contains(p.Id))
            .Select(p => new { p.Id, p.VoucherId, p.Date, Supplier = p.Supplier!.Name }).ToDictionaryAsync(p => p.Id);
        var returnIds = groups.Where(g => g.SalesReturn is not null).Select(g => g.SalesReturn!.Value).Distinct().ToList();
        var returns = await db.StockChanges.AsNoTracking().Where(s => returnIds.Contains(s.Id))
            .Select(s => new { s.Id, s.VoucherId, s.Date, Customer = s.Customer!.Name }).ToDictionaryAsync(s => s.Id);

        return groups.Select(g =>
        {
            var product = products.GetValueOrDefault(g.Product);
            // Batch 2-7: damage found in the shop links to its stock change entry.
            var variant = variants.GetValueOrDefault(g.Variant);
            string from;
            string? link = null;
            if (g.Purchase is { } p && purchases.TryGetValue(p, out var purchase))
            {
                from = $"Purchase {purchase.VoucherId} of {purchase.Date:dd-MM-yyyy}, {purchase.Supplier}";
                link = $"/purchase/{p}?from=damage";   // Esc there comes back to the damage report
            }
            else if (g.SalesReturn is { } r && returns.TryGetValue(r, out var ret))
            {
                from = $"Sales return {ret.VoucherId} of {ret.Date:dd-MM-yyyy}, {ret.Customer}";
                link = $"/salesreturn/{r}?from=damage";
            }
            else
            {
                from = $"Damaged in the shop, entry {g.ShopVoucher} of {g.Since:dd-MM-yyyy}";
                link = $"/stockchange/{g.StockChange}?from=damage";
            }

            return new DamagedLot(g.Key, g.Product, product?.ProductId ?? "", product?.Name ?? "", g.Variant, variant?.VariantId ?? "", variant?.Name,
                g.Remaining, g.Since, from, link, string.Join(", ", g.Reasons), g.Supplier,
                g.Supplier is { } s ? suppliers.GetValueOrDefault(s) : null, g.Ready, g.Waiting);
        })
        .OrderBy(l => l.Since).ThenBy(l => l.ProductName).ThenBy(l => l.VariantId)
        .ToList();
    }

    /// <summary>One group of damage lines belonging to the same purchase line / returned line / shop damage entry.</summary>
    private sealed record Group(
        Guid Key, Guid Product, Guid Variant, int Remaining, DateOnly Since, List<string> Reasons, Guid? Supplier, bool Ready,
        Guid? Purchase, Guid? SalesReturn, Guid FirstDamageLine, List<Guid> UpLines, int Waiting, Guid StockChange, int ShopVoucher);

    /// <summary>
    /// The damage lines, grouped by what they were found on (a purchase line, a returned line, or a damage entry made in the
    /// shop). Remaining: approved damage minus what later stock changes took from it (repaired, written off, sent back –
    /// approved or waiting, so waiting ones are not taken twice). Waiting: pieces on stock changes still waiting for approval.
    /// Rejected ones count for nothing. Groups with nothing left and nothing waiting are left out.
    /// </summary>
    private static async Task<List<Group>> GroupsAsync(AppDbContext db, IReadOnlyCollection<Guid>? keys = null)
    {
        var lines = await db.StockChangeLines.AsNoTracking()
            .Where(d => d.StockChange!.Kind == StockChangeKind.Damage && d.StockChange.Status != StockChangeStatus.Rejected)
            .Select(d => new
            {
                d.Id, d.PurchaseVariantId, d.SourceLineId, d.ProductEntityId, d.VariantEntityId, d.To, d.Quantity, d.Reason,
                d.ReturnSupplierEntityId, d.ReadyToSend, d.StockChange!.Date, d.StockChange.PurchaseEntityId, d.StockChange.SourceStockChangeId, d.StockChange.CreatedAt,
                d.StockChange.Status, d.StockChangeEntityId, d.StockChange.VoucherId,
            })
            .ToListAsync();
        var grouped = lines.GroupBy(d => d.PurchaseVariantId ?? d.SourceLineId ?? d.Id)
            .Where(g => keys is null || keys.Contains(g.Key))
            .ToList();
        if (grouped.Count == 0)
        {
            return [];
        }

        var damageIds = grouped.SelectMany(g => g).Select(d => d.Id).ToList();
        var taken = (await db.StockChangeLines.AsNoTracking()
                .Where(t => t.SourceLineId != null && damageIds.Contains(t.SourceLineId.Value)
                            && t.StockChange!.Kind != StockChangeKind.Damage && t.StockChange.Status != StockChangeStatus.Rejected)
                .Select(t => new { Damage = t.SourceLineId!.Value, t.Quantity, t.StockChange!.Status })
                .ToListAsync())
            .GroupBy(t => t.Damage).ToDictionary(g => g.Key, g => (All: g.Sum(t => t.Quantity), Waiting: g.Where(t => t.Status == StockChangeStatus.Waiting).Sum(t => t.Quantity)));

        var result = new List<Group>();
        foreach (var g in grouped)
        {
            static int Signed(StockState to, int quantity) => to == StockState.Damaged ? quantity : -quantity;
            var approved = g.Where(d => d.Status == StockChangeStatus.Approved).Sum(d => Signed(d.To, d.Quantity));
            var waitingDamage = g.Where(d => d.Status == StockChangeStatus.Waiting).Sum(d => Math.Abs(Signed(d.To, d.Quantity)));
            var remaining = approved - g.Sum(d => taken.GetValueOrDefault(d.Id).All);
            var waiting = waitingDamage + g.Sum(d => taken.GetValueOrDefault(d.Id).Waiting);
            if (remaining <= 0 && waiting == 0)
            {
                continue;
            }

            var up = g.Where(d => d.To == StockState.Damaged).OrderBy(d => d.Date).ThenBy(d => d.CreatedAt).ToList();
            var latest = up.LastOrDefault() ?? g.First();
            var first = up.FirstOrDefault() ?? g.First();
            result.Add(new Group(
                g.Key, first.ProductEntityId, first.VariantEntityId, Math.Max(0, remaining), first.Date,
                up.Select(d => d.Reason).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!).Distinct().ToList(),
                latest.ReturnSupplierEntityId, latest.ReadyToSend, first.PurchaseEntityId, first.SourceStockChangeId,
                first.Id, up.Select(d => d.Id).ToList(), waiting, first.StockChangeEntityId, first.VoucherId));
        }

        return result;
    }

    /// <summary>The suppliers for the Supplier drop-down of each row, by name.</summary>
    public async Task<List<(Guid Id, string Name)>> SuppliersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return (await db.Suppliers.AsNoTracking().OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync())
            .Select(s => (s.Id, s.Name ?? "")).ToList();
    }

    /// <summary>Assigns the supplier the damaged goods of these rows go back to (Hiten: filled in from the purchase, can be changed).</summary>
    public async Task AssignSupplierAsync(IReadOnlyCollection<Guid> keys, Guid supplierId)
    {
        if (supplierId == Guid.Empty)
        {
            throw new StockChangeRefused("Please select a supplier.");
        }

        await SetOnUpLinesAsync(keys, l => l.ReturnSupplierEntityId = supplierId);
    }

    /// <summary>Marks these rows "Ready to send", or back to "Supplier assigned" (Status drop-down). A row needs a supplier to be ready.</summary>
    public async Task SetReadyAsync(IReadOnlyCollection<Guid> keys, bool ready)
    {
        if (ready)
        {
            await using var db = await factory.CreateDbContextAsync();
            if ((await GroupsAsync(db, keys)).Any(g => g.Supplier is null))
            {
                throw new StockChangeRefused("Assign a supplier first: goods are made ready to send to a supplier.");
            }
        }

        await SetOnUpLinesAsync(keys, l => l.ReadyToSend = ready);
    }

    private async Task SetOnUpLinesAsync(IReadOnlyCollection<Guid> keys, Action<StockChangeLineEntity> set)
    {
        if (keys.Count == 0)
        {
            throw new StockChangeRefused("Tick the rows first.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var groups = await GroupsAsync(db, keys);
        var ids = groups.SelectMany(g => g.UpLines).ToList();
        foreach (var line in await db.StockChangeLines.Where(l => ids.Contains(l.Id)).ToListAsync())
        {
            set(line);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Repaired: the pieces go back to sellable stock.</summary>
    public Task<StockChangeEntity> RepairAsync(IReadOnlyList<LotPieces> lots, string? note = null) =>
        TakeFromDamageAsync(lots, StockChangeKind.Repaired, StockState.Sellable, null, note);

    /// <summary>Written off: the pieces leave stock (thrown away or lost).</summary>
    public Task<StockChangeEntity> WriteOffAsync(IReadOnlyList<LotPieces> lots, string? note = null) =>
        TakeFromDamageAsync(lots, StockChangeKind.WriteOff, StockState.Outside, null, note);

    /// <summary>
    /// Sent: the pieces go back to their supplier on a new Purchase Return voucher (one per supplier per sending, Hiten), and
    /// leave the damaged count. All rows must have the same supplier.
    /// </summary>
    public async Task<StockChangeEntity> SendBackAsync(IReadOnlyList<LotPieces> lots, string? note = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var groups = await GroupsAsync(db, lots.Select(l => l.Key).ToList());
        if (groups.Any(g => g.Supplier is null))
        {
            throw new StockChangeRefused("Assign a supplier first to every ticked row.");
        }

        var suppliers = groups.Select(g => g.Supplier).Distinct().ToList();
        if (suppliers.Count > 1)
        {
            throw new StockChangeRefused("The ticked goods go to different suppliers. Send them one supplier at a time (the Supplier filter helps).");
        }

        return await TakeFromDamageAsync(lots, StockChangeKind.PurchaseReturn, StockState.Outside, suppliers.SingleOrDefault(), note);
    }

    private async Task<StockChangeEntity> TakeFromDamageAsync(IReadOnlyList<LotPieces> lots, StockChangeKind kind, StockState to, Guid? supplier, string? note)
    {
        if (lots.Count == 0)
        {
            throw new StockChangeRefused("Tick the rows first.");
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Checked again at the moment of saving: two people cannot take the same damaged pieces twice.
        var groups = (await GroupsAsync(db, lots.Select(l => l.Key).ToList())).ToDictionary(g => g.Key);
        var today = Today;
        var change = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Date = today,
            VoucherId = await StockChangeService.NextVoucherAsync(db, kind, today),
            Status = await StockChangeService.NewStatusAsync(db),
            SupplierEntityId = supplier,
            Remarks = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        var serial = 1;
        foreach (var lot in lots)
        {
            if (!groups.TryGetValue(lot.Key, out var group))
            {
                throw new StockChangeRefused("Some of these goods are no longer lying in damage. Reload the report.");
            }

            if (lot.Pieces <= 0 || lot.Pieces > group.Remaining)
            {
                throw new StockChangeRefused(lot.Pieces <= 0
                    ? "The pieces must be more than 0."
                    : $"Only {group.Remaining} piece{(group.Remaining == 1 ? " is" : "s are")} lying in damage on one of the rows.");
            }

            change.Lines.Add(new StockChangeLineEntity
            {
                Id = Guid.NewGuid(),
                SerialNumber = serial++,
                ProductEntityId = group.Product,
                VariantEntityId = group.Variant,
                Quantity = lot.Pieces,
                From = StockState.Damaged,
                To = to,
                SourceLineId = group.FirstDamageLine,   // the damaged lot it takes from
            });
        }

        db.StockChanges.Add(change);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return change;
    }

    // ---------------------------------------------------------------- purchase returns (batch 2-6)

    public async Task<List<PurchaseReturnListItem>> PurchaseReturnsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.StockChanges.AsNoTracking()
            .Where(s => s.Kind == StockChangeKind.PurchaseReturn)
            .Select(s => new
            {
                s.Id, s.VoucherId, s.Date, Supplier = s.Supplier!.Name, s.Remarks,
                FromDamage = s.Lines.Where(l => l.From == StockState.Damaged).Sum(l => (int?)l.Quantity) ?? 0,
                Good = s.Lines.Where(l => l.From == StockState.Sellable).Sum(l => (int?)l.Quantity) ?? 0,
            })
            .ToListAsync();
        return rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.VoucherId)
            .Select(r => new PurchaseReturnListItem(r.Id, r.VoucherId, r.Date, r.Supplier ?? "", r.FromDamage, r.Good, r.Remarks)).ToList();
    }

    /// <summary>A purchase return with its supplier and lines, or null. Remembers the version opened (edit clash).</summary>
    public async Task<StockChangeEntity?> GetPurchaseReturnAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var found = await db.StockChanges
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(s => s.Supplier)
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .FirstOrDefaultAsync(s => s.Id == id && s.Kind == StockChangeKind.PurchaseReturn);
        db.MarkSeen(found);
        return found;
    }

    /// <summary>Where each line of a purchase return that came from damage was found ("Purchase 12 of 25-11-2026").</summary>
    public async Task<Dictionary<Guid, string>> SourcesAsync(StockChangeEntity purchaseReturn)
    {
        var map = purchaseReturn.Lines.Where(l => l.SourceLineId is not null).ToDictionary(l => l.Id, l => l.SourceLineId!.Value);
        if (map.Count == 0)
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync();
        var damageIds = map.Values.Distinct().ToList();
        var damage = await db.StockChangeLines.AsNoTracking().Where(d => damageIds.Contains(d.Id))
            .Select(d => new { d.Id, d.StockChange!.PurchaseEntityId, d.StockChange.SourceStockChangeId }).ToListAsync();
        var purchaseIds = damage.Where(d => d.PurchaseEntityId != null).Select(d => d.PurchaseEntityId!.Value).ToList();
        var returnIds = damage.Where(d => d.SourceStockChangeId != null).Select(d => d.SourceStockChangeId!.Value).ToList();
        var purchases = await db.Purchases.AsNoTracking().Where(p => purchaseIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => $"Purchase {p.VoucherId} of {p.Date:dd-MM-yyyy}");
        var returns = await db.StockChanges.AsNoTracking().Where(s => returnIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => $"Sales return {s.VoucherId} of {s.Date:dd-MM-yyyy}");
        return map.ToDictionary(m => m.Key, m =>
        {
            var d = damage.FirstOrDefault(x => x.Id == m.Value);
            return d?.PurchaseEntityId is { } p ? purchases.GetValueOrDefault(p, "a purchase")
                : d?.SourceStockChangeId is { } r ? returns.GetValueOrDefault(r, "a sales return")
                : "found in the shop";
        });
    }

    public async Task<StockChangeEntity> CreatePurchaseReturnAsync(Guid supplierId, DateOnly date, string? remarks)
    {
        if (supplierId == Guid.Empty)
        {
            throw new StockChangeRefused("Please select a supplier.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var change = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = StockChangeKind.PurchaseReturn,
            Date = date,
            VoucherId = await StockChangeService.NextVoucherAsync(db, StockChangeKind.PurchaseReturn, date),
            Status = await StockChangeService.NewStatusAsync(db),
            SupplierEntityId = supplierId,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
        };
        db.StockChanges.Add(change);
        await db.SaveChangesAsync();
        return change;
    }

    /// <summary>Saves supplier, date and remarks. The voucher number stays.</summary>
    public async Task UpdatePurchaseReturnAsync(Guid id, Guid supplierId, DateOnly date, string? remarks)
    {
        if (supplierId == Guid.Empty)
        {
            throw new StockChangeRefused("Please select a supplier.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StockChanges.FirstOrDefaultAsync(s => s.Id == id && s.Kind == StockChangeKind.PurchaseReturn);
        if (existing is null)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(id);
            throw new StockChangeRefused("This purchase return was not found.");
        }

        existing.SupplierEntityId = supplierId;
        existing.Date = date;
        existing.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        await db.SaveChangesAsync();
    }

    /// <summary>Deletes a purchase return: good stock comes back to sellable, damaged goods back onto the damage report.</summary>
    public async Task DeletePurchaseReturnAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id && s.Kind == StockChangeKind.PurchaseReturn);
        if (existing is null)
        {
            return;
        }

        db.StockChanges.Remove(existing);
        await db.SaveChangesAsync();
    }

    /// <summary>Good stock sent back to the supplier (not from damage): Sellable → Outside.</summary>
    public async Task AddPurchaseReturnLinesAsync(Guid returnId, IReadOnlyList<(Guid ProductId, Guid VariantId, int? Quantity, int SerialNumber)> lines)
    {
        foreach (var l in lines)
        {
            if (StockChangeRules.ReturnLineProblem(l.ProductId, l.VariantId, l.Quantity, "The quantity sent back must be more than 0.") is { } problem)
            {
                throw new StockChangeRefused(problem);
            }
        }

        await using var db = await factory.CreateDbContextAsync();
        var header = await db.StockChanges.FirstOrDefaultAsync(s => s.Id == returnId && s.Kind == StockChangeKind.PurchaseReturn);
        if (header is null)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(returnId);
            throw new StockChangeRefused("This purchase return was not found.");
        }

        await StockChangeService.NeedsApprovalAgainAsync(db, header);

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
                From = StockState.Sellable,
                To = StockState.Outside,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Changes a line of good stock sent back (a line that came from damage is changed on the damage report instead).</summary>
    public async Task UpdatePurchaseReturnLineAsync(Guid lineId, Guid productId, Guid variantId, int? quantity, int serialNumber)
    {
        if (StockChangeRules.ReturnLineProblem(productId, variantId, quantity, "The quantity sent back must be more than 0.") is { } problem)
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

        if (existing.ProductEntityId != productId || existing.VariantEntityId != variantId || existing.Quantity != quantity)
        {
            await StockChangeService.NeedsApprovalAgainAsync(db, await db.StockChanges.FirstAsync(s => s.Id == existing.StockChangeEntityId));
        }

        existing.ProductEntityId = productId;
        existing.VariantEntityId = variantId;
        existing.Quantity = quantity!.Value;
        existing.SerialNumber = serialNumber;
        await db.SaveChangesAsync();
    }

    /// <summary>Removes a line: good stock comes back to sellable; damaged goods go back onto the damage report.</summary>
    public async Task DeletePurchaseReturnLineAsync(Guid lineId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StockChangeLines.FirstOrDefaultAsync(l => l.Id == lineId);
        if (existing is null)
        {
            return;
        }

        await StockChangeService.NeedsApprovalAgainAsync(db, await db.StockChanges.FirstAsync(s => s.Id == existing.StockChangeEntityId));
        db.StockChangeLines.Remove(existing);
        await db.SaveChangesAsync();
    }
}
