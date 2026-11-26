using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 3, batch 3-1 (Hiten, 26 Nov 2026): stock is counted per place (the shop, the godown); every purchase, sale and
/// stock change line says where; everything made before stage 3 (and everything made by today's screens) is in the shop.
/// The places of a variant always add up to its sellable figure.
/// </summary>
public class PlacesTests
{
    private static readonly DateOnly Day = new(2030, 3, 10);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<PurchaseEntity> PurchaseAsync(TestDatabase db, Seed seed, Guid variant, int qty, int? place = null)
    {
        await using var c = db.Open();
        var purchase = new PurchaseEntity
        {
            Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1,
            PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = variant, SerialNumber = 1, Quantity = qty }],
        };
        if (place is { } p)
        {
            purchase.PlaceId = p;
        }

        c.Purchases.Add(purchase);
        await c.SaveChangesAsync(Ct);
        return purchase;
    }

    private static async Task<SalesEntity> SellAsync(TestDatabase db, Seed seed, Guid variant, int qty, int? place = null)
    {
        await using var c = db.Open();
        var sale = new SalesEntity
        {
            Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1,
            SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = variant, Quantity = qty }],
        };
        if (place is { } p)
        {
            sale.PlaceId = p;
        }

        c.Sales.Add(sale);
        await c.SaveChangesAsync(Ct);
        return sale;
    }

    private static async Task<StockChangeEntity> ChangeAsync(TestDatabase db, Seed seed, StockChangeStatus status, StockChangeLineEntity line)
    {
        await using var c = db.Open();
        line.SerialNumber = 1;
        line.ProductEntityId = seed.ProductId;
        var change = new StockChangeEntity { Kind = StockChangeKind.Damage, Date = Day, VoucherId = 1, Status = status, Lines = [line] };
        c.StockChanges.Add(change);
        await c.SaveChangesAsync(Ct);
        return change;
    }

    private static async Task<Dictionary<int, int>> PlacesOf(TestDatabase db, Guid variant)
    {
        await using var c = db.Open();
        return (await StockQuery.SellableByPlaceAsync(c, [variant])).GetValueOrDefault(variant) ?? new Dictionary<int, int>();
    }

    private static async Task<int> SellableOf(TestDatabase db, Guid variant)
    {
        await using var c = db.Open();
        return (await StockQuery.ForVariantsAsync(c, [variant])).GetValueOrDefault(variant).Sellable;
    }

    [Fact]
    public async Task The_upgrade_makes_the_shop_and_the_godown()
    {
        using var db = new TestDatabase();
        await using var c = db.Open();
        var places = await c.Places.OrderBy(p => p.Number).Select(p => new { p.Id, p.Number, p.Name }).ToListAsync(Ct);
        Assert.Equal([new { Id = Places.StartingId, Number = 1, Name = "Shop" }, new { Id = Places.GodownId, Number = 2, Name = "Godown" }], places);
    }

    [Fact]
    public async Task Purchases_and_sales_made_as_today_are_in_the_shop_and_all_stock_is_there()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchase = await PurchaseAsync(db, seed, seed.Variant1Id, 10);
        var sale = await SellAsync(db, seed, seed.Variant1Id, 3);

        await using var c = db.Open();
        Assert.Equal(Places.StartingId, (await c.Purchases.FindAsync([purchase.Id], Ct))!.PlaceId);
        Assert.Equal(Places.StartingId, (await c.Sales.FindAsync([sale.Id], Ct))!.PlaceId);
        Assert.Equal(new Dictionary<int, int> { [Places.StartingId] = 7 }, await PlacesOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Stock_is_counted_per_place_and_the_places_add_up_to_the_sellable_figure()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 10, Places.GodownId);
        await PurchaseAsync(db, seed, seed.Variant1Id, 4);              // shop
        await SellAsync(db, seed, seed.Variant1Id, 6);                  // from the shop: it goes below zero (Hiten: allowed)
        await SellAsync(db, seed, seed.Variant1Id, 1, Places.GodownId); // a bulk order straight from the godown

        var places = await PlacesOf(db, seed.Variant1Id);
        Assert.Equal(-2, places[Places.StartingId]);
        Assert.Equal(9, places[Places.GodownId]);
        Assert.Equal(await SellableOf(db, seed.Variant1Id), places.Values.Sum());
    }

    [Fact]
    public async Task A_stock_change_line_takes_and_gives_sellable_pieces_in_its_own_place()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 10, Places.GodownId);
        await ChangeAsync(db, seed, StockChangeStatus.Approved, new StockChangeLineEntity
        {
            VariantEntityId = seed.Variant1Id, Quantity = 4, From = StockState.Sellable, FromPlaceId = Places.GodownId, To = StockState.Sellable, ToPlaceId = Places.StartingId,
        });
        await ChangeAsync(db, seed, StockChangeStatus.Approved, new StockChangeLineEntity
        {
            VariantEntityId = seed.Variant1Id, Quantity = 1, From = StockState.Sellable, FromPlaceId = Places.GodownId, To = StockState.Damaged,
        });

        var places = await PlacesOf(db, seed.Variant1Id);
        Assert.Equal(4, places[Places.StartingId]);
        Assert.Equal(5, places[Places.GodownId]);
        Assert.Equal(9, await SellableOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task A_line_from_sellable_to_sellable_in_the_same_place_is_refused()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ChangeAsync(db, seed, StockChangeStatus.Approved, new StockChangeLineEntity
        {
            VariantEntityId = seed.Variant1Id, Quantity = 1, From = StockState.Sellable, FromPlaceId = Places.GodownId, To = StockState.Sellable, ToPlaceId = Places.GodownId,
        }));
        Assert.Contains("RULE: A stock change line must move", ex.InnerException!.Message);
        await Assert.ThrowsAsync<DbUpdateException>(() => ChangeAsync(db, seed, StockChangeStatus.Approved, new StockChangeLineEntity
        {
            VariantEntityId = seed.Variant1Id, Quantity = 1, From = StockState.Damaged, To = StockState.Damaged,
        }));
    }

    [Fact]
    public async Task A_waiting_or_rejected_stock_change_moves_nothing_in_any_place()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 10, Places.GodownId);
        foreach (var status in new[] { StockChangeStatus.Waiting, StockChangeStatus.Rejected })
        {
            await ChangeAsync(db, seed, status, new StockChangeLineEntity
            {
                VariantEntityId = seed.Variant1Id, Quantity = 3, From = StockState.Sellable, FromPlaceId = Places.GodownId, To = StockState.Damaged,
            });
        }

        Assert.Equal(new Dictionary<int, int> { [Places.StartingId] = 0, [Places.GodownId] = 10 }, await PlacesOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task A_stock_change_line_made_without_a_place_is_in_the_shop()
    {
        // Every stage 2 screen still makes lines without a place (batch 3-4 gives them their own); they land in the shop.
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var change = await ChangeAsync(db, seed, StockChangeStatus.Approved, new StockChangeLineEntity
        {
            VariantEntityId = seed.Variant1Id, Quantity = 2, From = StockState.Outside, To = StockState.Sellable,
        });

        await using var c = db.Open();
        var line = await c.StockChangeLines.SingleAsync(l => l.StockChangeEntityId == change.Id, Ct);
        Assert.Null(line.FromPlaceId);                 // outside has no place
        Assert.Equal(Places.StartingId, line.ToPlaceId);
        Assert.Equal(new Dictionary<int, int> { [Places.StartingId] = 2 }, await PlacesOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task The_database_refuses_a_place_that_does_not_exist()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => PurchaseAsync(db, seed, seed.Variant1Id, 1, place: 99));
        await Assert.ThrowsAsync<DbUpdateException>(() => SellAsync(db, seed, seed.Variant1Id, 1, place: 99));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ChangeAsync(db, seed, StockChangeStatus.Approved, new StockChangeLineEntity
        {
            VariantEntityId = seed.Variant1Id, Quantity = 1, From = StockState.Outside, To = StockState.Sellable, ToPlaceId = 99,
        }));
        Assert.Contains("RULE: Stock must be in a real place.", ex.InnerException!.Message);
    }

    [Fact]
    public async Task A_place_that_holds_entries_or_the_shop_cannot_be_deleted_but_an_unused_one_can()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 1, Places.GodownId);
        await using var c = db.Open();
        c.Places.Add(new PlaceEntity { Number = 3, Name = "Second godown" });
        await c.SaveChangesAsync(Ct);

        foreach (var id in new[] { Places.StartingId, Places.GodownId })
        {
            await using var d = db.Open();
            d.Places.Remove((await d.Places.FindAsync([id], Ct))!);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => d.SaveChangesAsync(Ct));
            Assert.Contains("RULE: A place that holds stock entries cannot be deleted.", ex.InnerException!.Message);
        }

        await using var e = db.Open();
        e.Places.Remove(await e.Places.SingleAsync(p => p.Number == 3, Ct));
        await e.SaveChangesAsync(Ct);
        Assert.Equal(2, await e.Places.CountAsync(Ct));
    }

    [Fact]
    public async Task A_variant_can_be_on_several_racks_and_a_rack_level_holds_several_variants()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using var c = db.Open();
        var rack = new RackEntity { PlaceId = Places.GodownId, Number = 3, Levels = 4 };
        var other = new RackEntity { PlaceId = Places.StartingId, Number = 3, Levels = 2 };   // same number in another place is fine
        c.Racks.AddRange(rack, other);
        await c.SaveChangesAsync(Ct);
        c.VariantRacks.AddRange(
            new VariantRackEntity { VariantEntityId = seed.Variant1Id, RackId = rack.Id, Level = "B", IsMain = true },
            new VariantRackEntity { VariantEntityId = seed.Variant1Id, RackId = other.Id, Level = "A" },
            new VariantRackEntity { VariantEntityId = seed.Variant2Id, RackId = rack.Id, Level = "B" });
        await c.SaveChangesAsync(Ct);

        await using var d = db.Open();
        d.VariantRacks.Add(new VariantRackEntity { VariantEntityId = seed.Variant1Id, RackId = rack.Id, Level = "B" });
        await Assert.ThrowsAsync<DbUpdateException>(() => d.SaveChangesAsync(Ct));   // the same rack level twice for one variant

        await using var e = db.Open();
        e.Racks.Add(new RackEntity { PlaceId = Places.GodownId, Number = 3, Levels = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => e.SaveChangesAsync(Ct));   // rack 03 twice in the godown

        await using var f = db.Open();
        f.Racks.Remove((await f.Racks.FindAsync([rack.Id], Ct))!);
        await Assert.ThrowsAsync<DbUpdateException>(() => f.SaveChangesAsync(Ct));   // a rack that holds items stays
    }

    [Fact]
    public async Task Deleting_a_variant_takes_its_racks_with_it()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using var c = db.Open();
        var rack = new RackEntity { PlaceId = Places.GodownId, Number = 1, Levels = 3 };
        c.Racks.Add(rack);
        await c.SaveChangesAsync(Ct);
        c.VariantRacks.Add(new VariantRackEntity { VariantEntityId = seed.Variant2Id, RackId = rack.Id, Level = "C" });
        await c.SaveChangesAsync(Ct);

        await using var d = db.Open();
        d.Variants.Remove((await d.Variants.FindAsync([seed.Variant2Id], Ct))!);
        await d.SaveChangesAsync(Ct);
        Assert.Equal(0, await d.VariantRacks.CountAsync(Ct));
    }
}
