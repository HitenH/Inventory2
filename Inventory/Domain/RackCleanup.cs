using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain;

/// <summary>
/// Stage 3, batch 3-7 (Hiten, 27 Nov 2026, "Yes to your reco"): racks clear themselves in every place except the one sales leave
/// from (the shop). When a save takes an item's sellable stock in the godown from above 0 down to 0 – sent to the shop, sold from
/// the godown, written off, damaged – the item is taken off all its racks there (the main one too): the rack is free for other
/// goods and pickers are not sent to an empty rack. Only a save that empties the place does it: a rack set before any stock was
/// there (all stock started in the shop) stays. The shop's racks are fixed and only change by hand.
/// Runs inside every save (<see cref="AppDbContext.SaveChangesAsync(bool, CancellationToken)"/>), for the items whose stock that
/// save touched, so no screen or future device can forget it. Each rack cleared is written into the product's history by the
/// app itself (no person's name).
/// </summary>
public static class RackCleanup
{
    /// <summary>The variants whose stock the pending changes may lower (lines added, changed or removed; a stock change decided).</summary>
    public static HashSet<Guid> Collect(AppDbContext db)
    {
        var ids = new HashSet<Guid>();
        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case SalesVariantEntity s:
                    ids.Add(s.VariantEntityId);
                    break;
                case PurchaseVariant p:
                    ids.Add(p.VariantEntityId);
                    break;
                case StockChangeLineEntity l:
                    ids.Add(l.VariantEntityId);
                    break;
                case StockChangeEntity c when entry.State == EntityState.Modified:
                    // Approved or rejected later: its lines start (or stop) counting.
                    foreach (var line in c.Lines)
                    {
                        ids.Add(line.VariantEntityId);
                    }

                    if (c.Lines.Count == 0)
                    {
                        ids.UnionWith(db.StockChangeLines.AsNoTracking().Where(l => l.StockChangeEntityId == c.Id).Select(l => l.VariantEntityId).ToList());
                    }

                    break;
            }
        }

        return ids;
    }

    /// <summary>
    /// Before a save: of the touched items, those kept on a rack outside the sales place, with their stock per place now. Null when
    /// none is (most saves – then nothing more is read).
    /// </summary>
    public static async Task<Dictionary<Guid, Dictionary<int, int>>?> BeforeAsync(AppDbContext db, IReadOnlyCollection<Guid> variantIds)
    {
        if (variantIds.Count == 0)
        {
            return null;
        }

        var salesPlace = await PlaceSettings.SalesPlaceAsync(db);
        var racked = await db.VariantRacks.AsNoTracking()
            .Where(v => variantIds.Contains(v.VariantEntityId) && v.Rack!.PlaceId != salesPlace)
            .Select(v => v.VariantEntityId).Distinct().ToListAsync();
        return racked.Count == 0 ? null : await StockQuery.SellableByPlaceAsync(db, racked);
    }

    /// <summary>
    /// After the save: takes the items off their racks in every place except the sales place that this save emptied (stock above 0
    /// before, 0 or less now). True when it removed any.
    /// </summary>
    public static async Task<bool> ClearAsync(AppDbContext db, Dictionary<Guid, Dictionary<int, int>> before)
    {
        var variantIds = before.Keys.ToList();
        var salesPlace = await PlaceSettings.SalesPlaceAsync(db);
        var racks = await db.VariantRacks
            .Include(v => v.Rack).ThenInclude(r => r!.Place)
            .Include(v => v.Variant).ThenInclude(v => v!.Product)
            .Where(v => variantIds.Contains(v.VariantEntityId) && v.Rack!.PlaceId != salesPlace)
            .ToListAsync();
        if (racks.Count == 0)
        {
            return false;
        }

        var stock = await StockQuery.SellableByPlaceAsync(db, variantIds);
        int Qty(Dictionary<Guid, Dictionary<int, int>> s, VariantRackEntity r) => s.GetValueOrDefault(r.VariantEntityId)?.GetValueOrDefault(r.Rack!.PlaceId) ?? 0;
        var emptied = racks.Where(r => Qty(before, r) > 0 && Qty(stock, r) <= 0).ToList();
        if (emptied.Count == 0)
        {
            return false;
        }

        var now = DateTime.Now;
        foreach (var group in emptied.GroupBy(r => r.VariantEntityId))
        {
            var first = group.First();
            var variant = first.Variant!;
            var addresses = string.Join(", ", group.OrderBy(r => r.Rack!.Number).ThenBy(r => r.Level)
                .Select(r => $"{r.Rack!.Place!.Number:00}-{r.Rack.Number:00}-{r.Level}"));
            db.ActivityLog.Add(new ActivityEntry
            {
                At = now, By = null, SaveId = Guid.NewGuid(), Action = "Changed",
                Text = $"Variant {variant.VariantId}: {(group.Count() == 1 ? "rack" : "racks")} {addresses} cleared (nothing left in {first.Rack!.Place!.Name})",
                RecordType = nameof(ProductEntity), RecordId = variant.ProductEntityId?.ToString(),
                RecordLabel = $"Product {variant.Product?.Name} ({variant.Product?.ProductId})",
            });
        }

        db.VariantRacks.RemoveRange(emptied);
        return true;
    }
}
