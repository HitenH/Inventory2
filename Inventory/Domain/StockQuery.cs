using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain;

/// <summary>
/// Stage 2: the stock of one variant. <see cref="Sellable"/> is what "Stock In Hand" shows (Hiten, 25 Nov 2026:
/// only sellable pieces, damaged counted separately: "13 + 3 damaged"). <see cref="Waiting"/> is the pieces on stock
/// changes waiting for approval; they have not moved yet.
/// </summary>
/// <remarks>
/// Stage 3 (Hiten, 26 Nov 2026: "it is sellable, just on the way … they just have to be reported separately"): pieces on the
/// way between two places are part of <see cref="Sellable"/>; <see cref="OnTheWay"/> says how many of them are on the way.
/// </remarks>
public readonly record struct StockFigures(int Sellable, int Damaged, int Waiting, int OnTheWay = 0)
{
    /// <summary>True when there is more to show than the sellable figure.</summary>
    public bool HasBreakdown => Damaged != 0 || Waiting != 0 || OnTheWay != 0;
}

/// <summary>Stage 3, batch 3-3: the sellable pieces of one variant in one place, for the breakdown under a stock figure ("Shop 4").</summary>
public readonly record struct PlaceStock(string Name, int Qty);

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

        var result = new Dictionary<Guid, (int Sellable, int Damaged, int Waiting, int OnTheWay)>();
        void Add(Guid variant, int sellable, int damaged, int waiting, int onTheWay = 0)
        {
            var now = result.GetValueOrDefault(variant);
            result[variant] = (now.Sellable + sellable, now.Damaged + damaged, now.Waiting + waiting, now.OnTheWay + onTheWay);
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

            // Stage 3: on the way is still sellable (Hiten), only reported separately.
            Add(m.VariantEntityId, Effect(m.To, StockState.Sellable, m.Qty) + Effect(m.To, StockState.InTransit, m.Qty)
                                   - Effect(m.From, StockState.Sellable, m.Qty) - Effect(m.From, StockState.InTransit, m.Qty),
                Effect(m.To, StockState.Damaged, m.Qty) - Effect(m.From, StockState.Damaged, m.Qty), 0,
                Effect(m.To, StockState.InTransit, m.Qty) - Effect(m.From, StockState.InTransit, m.Qty));
        }

        return result.ToDictionary(r => r.Key, r => new StockFigures(r.Value.Sellable, r.Value.Damaged, r.Value.Waiting, r.Value.OnTheWay));
    }

    private static int Effect(StockState state, StockState counted, int qty) => state == counted ? qty : 0;

    /// <summary>
    /// Stage 3, batch 3-3: like <see cref="ForVariantsAsync"/>, but the sellable figure counts only the pieces in
    /// <paramref name="places"/> (a godown person sees godown stock only). Damaged and waiting pieces have no place and stay
    /// as they are. Null places = every place, exactly <see cref="ForVariantsAsync"/>.
    /// </summary>
    public static async Task<Dictionary<Guid, StockFigures>> ForVariantsInPlacesAsync(AppDbContext context, IReadOnlyCollection<Guid>? variantIds, IReadOnlySet<int>? places)
    {
        var totals = await ForVariantsAsync(context, variantIds);
        if (places is null)
        {
            return totals;
        }

        var byPlace = await SellableByPlaceAsync(context, variantIds);
        return totals.Keys.Union(byPlace.Keys).ToDictionary(v => v, v =>
        {
            var all = totals.GetValueOrDefault(v);
            var mine = (byPlace.GetValueOrDefault(v) ?? new Dictionary<int, int>()).Where(p => places.Contains(p.Key)).Sum(p => p.Value);
            return all with { Sellable = mine };
        });
    }

    /// <summary>
    /// Stage 3: the sellable pieces of each variant per place (place id → pieces). The places of a variant always add up
    /// to its sellable figure from <see cref="ForVariantsAsync"/>: the entries in places other than the starting place (the
    /// shop) are added up – purchases in that place, minus sales from it, plus approved stock-change pieces moved into
    /// Sellable there, minus those moved out – and the starting place holds the rest. Most entries are in the shop, so
    /// only the few elsewhere are read line by line (timed on a copy of real data: 40,000 sale lines, ~0 ms when 1 in 50
    /// sales is elsewhere, 80 ms when half are). Pass null for every variant; a variant with no entries is left out.
    /// </summary>
    public static async Task<Dictionary<Guid, Dictionary<int, int>>> SellableByPlaceAsync(AppDbContext context, IReadOnlyCollection<Guid>? variantIds)
    {
        var totals = await ForVariantsAsync(context, variantIds);

        var elsewherePurchases = context.Purchases.Where(p => p.PlaceId != Places.StartingId).Select(p => p.Id);
        var elsewhereSales = context.Sales.Where(s => s.PlaceId != Places.StartingId).Select(s => s.Id);
        var purchaseLines = context.PurchaseVariant.AsNoTracking().Where(pv => elsewherePurchases.Contains(pv.PurchaseEntityId));
        var salesLines = context.SalesVariants.AsNoTracking().Where(sv => elsewhereSales.Contains(sv.SalesEntityId));
        var changeLines = context.StockChangeLines.AsNoTracking().Where(l => l.StockChange!.Status == StockChangeStatus.Approved);
        if (variantIds is not null)
        {
            purchaseLines = purchaseLines.Where(pv => variantIds.Contains(pv.VariantEntityId));
            salesLines = salesLines.Where(sv => variantIds.Contains(sv.VariantEntityId));
            changeLines = changeLines.Where(l => variantIds.Contains(l.VariantEntityId));
        }

        var purchased = await purchaseLines
            .GroupBy(pv => new { pv.VariantEntityId, pv.Purchase.PlaceId })
            .Select(g => new { g.Key.VariantEntityId, g.Key.PlaceId, Qty = g.Sum(x => x.Quantity ?? 0) })
            .ToListAsync();
        var sold = await salesLines
            .GroupBy(sv => new { sv.VariantEntityId, sv.Sale.PlaceId })
            .Select(g => new { g.Key.VariantEntityId, g.Key.PlaceId, Qty = g.Sum(x => x.Quantity ?? 0) })
            .ToListAsync();
        var movedIn = await changeLines
            .Where(l => l.To == StockState.Sellable && l.ToPlaceId != null && l.ToPlaceId != Places.StartingId)
            .GroupBy(l => new { l.VariantEntityId, l.ToPlaceId })
            .Select(g => new { g.Key.VariantEntityId, PlaceId = g.Key.ToPlaceId!.Value, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();
        var movedOut = await changeLines
            .Where(l => l.From == StockState.Sellable && l.FromPlaceId != null && l.FromPlaceId != Places.StartingId)
            .GroupBy(l => new { l.VariantEntityId, l.FromPlaceId })
            .Select(g => new { g.Key.VariantEntityId, PlaceId = g.Key.FromPlaceId!.Value, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var elsewhere = new Dictionary<Guid, Dictionary<int, int>>();
        void Add(Guid variant, int place, int qty)
        {
            if (!elsewhere.TryGetValue(variant, out var places))
            {
                elsewhere[variant] = places = new Dictionary<int, int>();
            }

            places[place] = places.GetValueOrDefault(place) + qty;
        }

        purchased.ForEach(p => Add(p.VariantEntityId, p.PlaceId, p.Qty));
        sold.ForEach(s => Add(s.VariantEntityId, s.PlaceId, -s.Qty));
        movedIn.ForEach(m => Add(m.VariantEntityId, m.PlaceId, m.Qty));
        movedOut.ForEach(m => Add(m.VariantEntityId, m.PlaceId, -m.Qty));

        var result = new Dictionary<Guid, Dictionary<int, int>>();
        foreach (var variant in totals.Keys.Union(elsewhere.Keys))
        {
            var places = elsewhere.GetValueOrDefault(variant) ?? new Dictionary<int, int>();
            // Pieces on the way are sellable but in no place: the starting place holds the rest without them.
            var total = totals.GetValueOrDefault(variant);
            places[Places.StartingId] = total.Sellable - total.OnTheWay - places.Values.Sum();
            result[variant] = places;
        }

        return result;
    }
}

/// <summary>Stage 3: the place a stock change line's sellable pieces are in.</summary>
public static class StockPlaces
{
    /// <summary>
    /// Stage 3: a stock change line whose sellable side has no place of its own gets its entry's place
    /// (<see cref="StockChangeEntity.PlaceId"/>, set by each kind of entry by Hiten's rules, batch 3-4), or the starting place
    /// (the shop) when the entry has none. Moves between places (batch 3-5) give their lines both places themselves.
    /// Runs before every save (<see cref="AppDbContext.SaveChangesAsync(bool, CancellationToken)"/>).
    /// </summary>
    public static void FillPlaces(AppDbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<StockChangeLineEntity>().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var line = entry.Entity;
            var needsFrom = line.From == StockState.Sellable && line.FromPlaceId is null;
            var needsTo = line.To == StockState.Sellable && line.ToPlaceId is null;
            if (!needsFrom && !needsTo)
            {
                continue;
            }

            var header = line.StockChange ?? context.StockChanges.Find(line.StockChangeEntityId);
            var place = header?.PlaceId ?? Places.StartingId;
            if (needsFrom)
            {
                line.FromPlaceId = place;
            }

            if (needsTo)
            {
                line.ToPlaceId = place;
            }
        }
    }
}

/// <summary>
/// Stage 3, batch 3-4 (Hiten, 26 Nov 2026): where purchases arrive (the godown) and where sales leave from (the shop; repaired
/// goods go back there too). Kept in the database (<see cref="AppSettingEntity"/>), changed by the Admin on Places and racks.
/// A setting pointing to a place that no longer exists falls back to the default.
/// </summary>
public static class PlaceSettings
{
    public const string PurchasesKey = "PurchasesArriveIn";
    public const string SalesKey = "SalesLeaveFrom";

    public static async Task<int> PurchasePlaceAsync(AppDbContext db) =>
        await ReadAsync(db, PurchasesKey) ?? (await db.Places.AnyAsync(p => p.Id == Places.GodownId) ? Places.GodownId : Places.StartingId);

    public static async Task<int> SalesPlaceAsync(AppDbContext db) => await ReadAsync(db, SalesKey) ?? Places.StartingId;

    private static async Task<int?> ReadAsync(AppDbContext db, string key)
    {
        var text = await db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
        return int.TryParse(text, out var id) && await db.Places.AnyAsync(p => p.Id == id) ? id : null;
    }
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

    /// <summary>Hiten, 26 Nov 2026: the reason written when recorded damage is lowered (pieces go back to sellable).</summary>
    public const string NotDamaged = "Not damaged after all";
}
