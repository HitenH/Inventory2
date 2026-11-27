using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 3, batch 3-7 (Hiten, 27 Nov 2026, "Yes to your reco"): racks in every place except the shop clear themselves when a save
/// empties the item there; the shop's racks are fixed; a rack set before any stock was there stays.
/// </summary>
public class RackCleanupTests
{
    private static readonly DateOnly Day = new(2030, 6, 1);
    private const int Shop = Places.StartingId;
    private const int Godown = Places.GodownId;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly UserAccess Admin = new("Admin", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now);

    private static PlaceService Places_(TestDatabase db) => new(db.Factory, new ActivityLogService(db.Factory, new CurrentUser()), new CurrentUser());
    private static MoveService Moves(TestDatabase db) => new(db.Factory, new ActivityLogService(db.Factory, new CurrentUser()));

    /// <summary>Racks 02-01 (godown) and 01-01 (shop) with V1 on 02-01-A, 02-01-B and 01-01-A.</summary>
    private static async Task RacksAsync(TestDatabase db, Seed seed)
    {
        var places = Places_(db);
        await places.AddRacksAsync(Godown, 1, 1, 2);
        await places.AddRacksAsync(Shop, 1, 1, 2);
        await places.AddVariantRackByAddressAsync(seed.Variant1Id, "0201A");
        await places.AddVariantRackByAddressAsync(seed.Variant1Id, "0201B");
        await places.AddVariantRackByAddressAsync(seed.Variant1Id, "0101A");
    }

    private static async Task PurchaseAsync(TestDatabase db, Seed seed, int qty)
    {
        var id = await new PurchaseRepositoryEF(db.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });   // arrives in the godown
        await new PurchaseVariantRepositoryEF(db.Factory).Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = qty });
    }

    private static async Task<string> RacksOf(TestDatabase db, Guid variant) =>
        string.Join(" ", ((await Places_(db).RacksOfVariantsAsync([variant])).GetValueOrDefault(variant) ?? []).Select(r => r.Address.Text));

    [Fact]
    public async Task Sending_everything_out_of_the_godown_clears_its_godown_racks_and_keeps_the_shop_rack()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, 5);
        await RacksAsync(db, seed);

        await Moves(db).SendAsync(Godown, Shop, Day, null, false, [(seed.ProductId, seed.Variant1Id, 3, null)], Admin);
        Assert.Equal("01-01-A 02-01-A 02-01-B", await RacksOf(db, seed.Variant1Id));   // 2 still in the godown: racks stay

        await Moves(db).SendAsync(Godown, Shop, Day, null, false, [(seed.ProductId, seed.Variant1Id, 2, null)], Admin);
        Assert.Equal("01-01-A", await RacksOf(db, seed.Variant1Id));   // the godown is empty: its racks are free again

        await using var c = db.Open();
        var log = await c.ActivityLog.Where(a => a.RecordType == nameof(ProductEntity) && a.Text.Contains("cleared")).Select(a => new { a.Text, a.By }).SingleAsync(Ct);
        Assert.Equal("Variant V1: racks 02-01-A, 02-01-B cleared (nothing left in Godown)", log.Text);
        Assert.Null(log.By);   // done by the app itself
    }

    [Fact]
    public async Task A_rack_set_before_any_stock_was_in_the_godown_stays()
    {
        // All stock starts in the shop: a godown rack set before the starting count must not vanish at the next sale.
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var c = db.Open())
        {
            c.Purchases.Add(new PurchaseEntity
            {
                Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1,   // old stock: in the shop
                PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 10 }],
            });
            await c.SaveChangesAsync(Ct);
        }

        await RacksAsync(db, seed);
        await new SaleRepositoryEF(db.Factory).Create(new SalesEntity
        {
            Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1,
            SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2 }],
        });
        Assert.Equal("01-01-A 02-01-A 02-01-B", await RacksOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Selling_the_last_godown_pieces_clears_the_godown_racks_too()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, 2);
        await RacksAsync(db, seed);
        await new SaleRepositoryEF(db.Factory).Create(new SalesEntity
        {
            Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1, PlaceId = Godown,   // a bulk order straight from the godown
            SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2 }],
        });
        Assert.Equal("01-01-A", await RacksOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task A_transfer_waiting_for_approval_clears_nothing_until_it_is_approved()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, 4);
        await RacksAsync(db, seed);
        var moves = Moves(db);
        await moves.SetApprovalAsync(true, Admin);
        var move = await moves.SendAsync(Godown, Shop, Day, null, false, [(seed.ProductId, seed.Variant1Id, 4, null)], Admin);
        Assert.Equal("01-01-A 02-01-A 02-01-B", await RacksOf(db, seed.Variant1Id));

        await new StockEntryService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser())).DecideAsync(move.Id, true, Admin);
        Assert.Equal("01-01-A", await RacksOf(db, seed.Variant1Id));
    }
}
