using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain;

/// <summary>
/// Stage 2: the stock of one variant. <see cref="Sellable"/> is what "Stock In Hand" shows (Hiten, 25 Nov 2026:
/// only sellable pieces, damaged counted separately: "13 + 3 damaged"). <see cref="Waiting"/> is the pieces on stock
/// changes waiting for approval; they have not moved yet.
/// </summary>
public readonly record struct StockFigures(int Sellable, int Damaged, int Waiting)
{
    /// <summary>True when there is more to show than the sellable figure.</summary>
    public bool HasBreakdown => Damaged != 0 || Waiting != 0;
}

/// <summary>
/// The one place stock is worked out (stage 2, "way B"). Nothing is stored; every time:
/// sellable = purchased − sold + approved stock-change pieces moved into Sellable − those moved out of it;
/// damaged = approved pieces moved into Damaged − those moved out of it.
/// Purchases and sales are read exactly as before, so a database without stock changes gives the same
/// figures as version 2.0. Every screen and report asks this, never its own copy.
/// </summary>
public static class StockQuery
{
    /// <summary>Stock per variant. Pass null for every variant; a variant with no entries is left out (its stock is 0).</summary>
    public static async Task<Dictionary<Guid, StockFigures>> ForVariantsAsync(AppDbContext context, IReadOnlyCollection<Guid>? variantIds)
    {
        var purchaseLines = context.PurchaseVariant.AsNoTracking();
        var salesLines = context.SalesVariants.AsNoTracking();
        var changeLines = context.StockChangeLines.AsNoTracking();
        if (variantIds is not null)
        {
            purchaseLines = purchaseLines.Where(pv => variantIds.Contains(pv.VariantEntityId));
            salesLines = salesLines.Where(sv => variantIds.Contains(sv.VariantEntityId));
            changeLines = changeLines.Where(l => variantIds.Contains(l.VariantEntityId));
        }

        var purchased = await purchaseLines
            .GroupBy(pv => pv.VariantEntityId)
            .Select(g => new { Variant = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
            .ToListAsync();

        var sold = await salesLines
            .GroupBy(sv => sv.VariantEntityId)
            .Select(g => new { Variant = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
            .ToListAsync();

        var moved = await changeLines
            .Where(l => l.StockChange!.Status != StockChangeStatus.Rejected)
            .GroupBy(l => new { l.VariantEntityId, l.From, l.To, l.StockChange!.Status })
            .Select(g => new { g.Key.VariantEntityId, g.Key.From, g.Key.To, g.Key.Status, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var result = new Dictionary<Guid, (int Sellable, int Damaged, int Waiting)>();
        void Add(Guid variant, int sellable, int damaged, int waiting)
        {
            var now = result.GetValueOrDefault(variant);
            result[variant] = (now.Sellable + sellable, now.Damaged + damaged, now.Waiting + waiting);
        }

        foreach (var p in purchased)
        {
            Add(p.Variant, p.Qty, 0, 0);
        }

        foreach (var s in sold)
        {
            Add(s.Variant, -s.Qty, 0, 0);
        }

        foreach (var m in moved)
        {
            if (m.Status == StockChangeStatus.Waiting)
            {
                Add(m.VariantEntityId, 0, 0, m.Qty);
                continue;
            }

            Add(m.VariantEntityId, Effect(m.To, StockState.Sellable, m.Qty) - Effect(m.From, StockState.Sellable, m.Qty),
                Effect(m.To, StockState.Damaged, m.Qty) - Effect(m.From, StockState.Damaged, m.Qty), 0);
        }

        return result.ToDictionary(r => r.Key, r => new StockFigures(r.Value.Sellable, r.Value.Damaged, r.Value.Waiting));
    }

    private static int Effect(StockState state, StockState counted, int qty) => state == counted ? qty : 0;
}

/// <summary>Stage 2: the reasons offered at first; the Admin changes the list later (batch 2-7).</summary>
public static class StockReasons
{
    public static readonly string[] Starting =
    [
        "Broken in transit",
        "Manufacturing defect",
        "Damaged in shop",
        "Wrong item received",
        "Count difference",
        "Other",
    ];
}
