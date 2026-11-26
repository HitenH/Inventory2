using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 3, batches 3-3 and 3-4 (Hiten, 26 Nov 2026): purchases arrive in the godown, sales leave from the shop (or the place
/// chosen in "From"), damage and returns follow Hiten's rules, users see the stock of their own places only.
/// </summary>
public class PlaceFlowTests
{
    private static readonly DateOnly Day = new(2030, 4, 2);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(Guid Purchase, Guid Line1)> PurchaseAsync(TestDatabase db, Seed seed, int qty = 10)
    {
        var id = await new PurchaseRepositoryEF(db.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await new PurchaseVariantRepositoryEF(db.Factory).Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = qty });
        await using var c = db.Open();
        return (id, await c.PurchaseVariant.Where(l => l.PurchaseEntityId == id).Select(l => l.Id).SingleAsync(Ct));
    }

    private static async Task<Dictionary<int, int>> PlacesOf(TestDatabase db, Guid variant)
    {
        await using var c = db.Open();
        return (await StockQuery.SellableByPlaceAsync(c, [variant])).GetValueOrDefault(variant) ?? new Dictionary<int, int>();
    }

    private static PlaceService Places(TestDatabase db)
    {
        var who = new CurrentUser { Name = "Admin" };
        return new PlaceService(db.Factory, new ActivityLogService(db.Factory, who), who);
    }

    // ---------------------------------------------------------------- 3-4: where purchases arrive and sales leave from

    [Fact]
    public async Task Purchases_arrive_in_the_godown_and_sales_leave_from_the_shop_unless_changed_in_Settings()
    {
        using var db = new TestDatabase();
        var places = Places(db);
        Assert.Equal((Inventory.Domain.Entities.Places.GodownId, Inventory.Domain.Entities.Places.StartingId), await places.FlowAsync());

        var store = await places.AddPlaceAsync("Store");
        await places.SetPurchasePlaceAsync(store.Id);
        await places.SetSalesPlaceAsync(Inventory.Domain.Entities.Places.GodownId);
        Assert.Equal((store.Id, Inventory.Domain.Entities.Places.GodownId), await places.FlowAsync());

        await places.DeletePlaceAsync(store.Id);   // a setting pointing to a place that is gone falls back to the godown
        Assert.Equal(Inventory.Domain.Entities.Places.GodownId, (await places.FlowAsync()).Purchases);
        await Assert.ThrowsAsync<PlaceRefused>(() => places.SetSalesPlaceAsync(99));
    }

    [Fact]
    public async Task A_new_purchase_arrives_in_the_godown_and_keeps_its_place_when_saved_again()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, _) = await PurchaseAsync(db, seed);
        Assert.Equal(new Dictionary<int, int> { [Inventory.Domain.Entities.Places.StartingId] = 0, [Inventory.Domain.Entities.Places.GodownId] = 10 }, await PlacesOf(db, seed.Variant1Id));

        await Places(db).SetPurchasePlaceAsync(Inventory.Domain.Entities.Places.StartingId);   // only new purchases follow the setting
        var repository = new PurchaseRepositoryEF(db.Factory);
        var loaded = await repository.GetById(purchase);
        loaded.Remarks = "changed";
        await repository.Update(new PurchaseEntity { Id = purchase, SupplierEntityId = seed.SupplierId, Date = Day, VoucherId = 1, Remarks = "changed" });

        await using var c = db.Open();
        Assert.Equal(Inventory.Domain.Entities.Places.GodownId, (await c.Purchases.FindAsync([purchase], Ct))!.PlaceId);
    }

    [Fact]
    public async Task A_sale_leaves_from_the_place_its_screen_gives_and_keeps_it_through_the_summary()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var sales = new SaleRepositoryEF(db.Factory);
        var fromGodown = await sales.Create(new SalesEntity
        {
            Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1, PlaceId = Inventory.Domain.Entities.Places.GodownId,
            SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 3 }],
        });
        var unknownPlace = await sales.Create(new SalesEntity
        {
            Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 2, PlaceId = 99,
            SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 1 }],
        });

        // The summary screen saves the sale it loaded: the place stays.
        var loaded = await sales.GetById(fromGodown);
        loaded.SalesVariants = null;
        await sales.Update(loaded);

        await using var c = db.Open();
        Assert.Equal(Inventory.Domain.Entities.Places.GodownId, (await c.Sales.FindAsync([fromGodown], Ct))!.PlaceId);
        Assert.Equal(Inventory.Domain.Entities.Places.StartingId, (await c.Sales.FindAsync([unknownPlace], Ct))!.PlaceId);   // where sales leave from
        Assert.Equal(new Dictionary<int, int> { [Inventory.Domain.Entities.Places.StartingId] = -1, [Inventory.Domain.Entities.Places.GodownId] = -3 }, await PlacesOf(db, seed.Variant1Id));
    }

    // ---------------------------------------------------------------- 3-4: damage, returns and repairs (Hiten's rules)

    [Fact]
    public async Task Damage_on_a_purchase_comes_out_of_the_place_it_arrived_in_and_a_repair_goes_back_to_the_shop()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line) = await PurchaseAsync(db, seed);
        await new StockChangeService(db.Factory).RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line, 3, "Broken in transit")], null);
        Assert.Equal(7, (await PlacesOf(db, seed.Variant1Id))[Inventory.Domain.Entities.Places.GodownId]);

        var damaged = new DamagedGoodsService(db.Factory);
        await damaged.RepairAsync([new LotPieces(line, 2)]);
        var places = await PlacesOf(db, seed.Variant1Id);
        Assert.Equal(7, places[Inventory.Domain.Entities.Places.GodownId]);
        Assert.Equal(2, places[Inventory.Domain.Entities.Places.StartingId]);   // Hiten: a repaired piece goes back to the shop
    }

    [Fact]
    public async Task A_sales_return_comes_back_into_the_shop_its_damage_leaves_the_shop_count_as_it_was_and_an_exchange_is_given_from_the_shop()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);   // 10 in the godown
        var service = new StockChangeService(db.Factory);
        var salesReturn = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await service.AddSalesReturnLineAsync(salesReturn.Id, seed.ProductId, seed.Variant1Id, 4, 1);
        Assert.Equal(4, (await PlacesOf(db, seed.Variant1Id))[Inventory.Domain.Entities.Places.StartingId]);

        var line = (await service.DamageCandidatesAsync(DamageSource.SalesReturn, salesReturn.Id)).Single().LineId;
        await service.RecordDamageAsync(DamageSource.SalesReturn, salesReturn.Id, [new DamageEntry(line, 4, null)], null);
        await service.AddExchangeLinesAsync(salesReturn.Id, [(seed.ProductId, seed.Variant1Id, 1)]);

        var places = await PlacesOf(db, seed.Variant1Id);
        Assert.Equal(-1, places[Inventory.Domain.Entities.Places.StartingId]);   // returned damaged: in and straight out; 1 given from the shop
        Assert.Equal(10, places[Inventory.Domain.Entities.Places.GodownId]);     // the godown is never touched
    }

    [Fact]
    public async Task Good_stock_sent_back_to_a_supplier_leaves_from_where_purchases_arrive()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var damaged = new DamagedGoodsService(db.Factory);
        var back = await damaged.CreatePurchaseReturnAsync(seed.SupplierId, Day, null);
        await damaged.AddPurchaseReturnLinesAsync(back.Id, [(seed.ProductId, seed.Variant1Id, 2, 1)]);
        Assert.Equal(8, (await PlacesOf(db, seed.Variant1Id))[Inventory.Domain.Entities.Places.GodownId]);
    }

    [Fact]
    public async Task A_stock_change_entry_counts_in_the_place_chosen_on_its_screen()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var entries = new StockEntryService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser()));
        var correction = await entries.CreateAsync(StockEntryType.CountCorrection, Day, null, null, Inventory.Domain.Entities.Places.GodownId);
        await entries.AddLinesAsync(correction.Id, [(seed.ProductId, seed.Variant1Id, -3, 1, null)]);
        var shopDamage = await entries.CreateAsync(StockEntryType.DamagedInShop, Day, null, null);   // no place given: where sales leave from
        await entries.AddLinesAsync(shopDamage.Id, [(seed.ProductId, seed.Variant1Id, 1, 1, null)]);

        var places = await PlacesOf(db, seed.Variant1Id);
        Assert.Equal(7, places[Inventory.Domain.Entities.Places.GodownId]);
        Assert.Equal(-1, places[Inventory.Domain.Entities.Places.StartingId]);
        await Assert.ThrowsAsync<StockChangeRefused>(() => entries.CreateAsync(StockEntryType.WrittenOff, Day, null, null, 99));
    }

    // ---------------------------------------------------------------- 3-3: each user's places

    [Fact]
    public async Task A_user_limited_to_the_godown_sees_godown_stock_only_and_the_admin_role_sees_every_place()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);   // 10 in the godown
        await using (var c = db.Open())
        {
            c.Sales.Add(new SalesEntity
            {
                Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1,
                SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 3 }],
            });
            c.Users.Add(new UserEntity { Name = "Ramesh", Password = "x" });
            await c.SaveChangesAsync(Ct);
            c.UserPlaces.Add(new UserPlaceEntity { UserId = c.Users.Single(u => u.Name == "Ramesh").Id, PlaceId = Inventory.Domain.Entities.Places.GodownId });
            await c.SaveChangesAsync(Ct);
        }

        await using var d = db.Open();
        var ramesh = await AccessService.LoadAsync(d, "Ramesh", DateTime.Now);
        Assert.Equal([Inventory.Domain.Entities.Places.GodownId], ramesh.Places!);
        Assert.False(ramesh.SeesPlace(Inventory.Domain.Entities.Places.StartingId));

        var products = new ProductRepositoryEF(db.Factory);
        Assert.Equal(10, (await products.GetStockByVariantIdsAsync([seed.Variant1Id], ramesh.Places))[seed.Variant1Id].Sellable);
        Assert.Equal(7, (await products.GetStockByVariantIdsAsync([seed.Variant1Id]))[seed.Variant1Id].Sellable);   // everyone else: all places
        Assert.Equal([new PlaceStock("Godown", 10)], (await products.GetPlaceStockAsync([seed.Variant1Id], ramesh.Places))[seed.Variant1Id]);
        Assert.Equal([new PlaceStock("Shop", -3), new PlaceStock("Godown", 10)], (await products.GetPlaceStockAsync([seed.Variant1Id]))[seed.Variant1Id]);
        Assert.Equal(10, (await products.GetProductStockReportAsync(places: ramesh.Places)).Single(r => r.VariantId == "V1").StockInHand);

        var admin = new UserAccess("Boss", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now, new HashSet<int> { 2 });
        Assert.Null(admin.Places);   // the Admin role always has every place
    }

    [Fact]
    public async Task Places_are_given_to_a_user_on_the_Users_screen_and_all_ticked_means_every_place()
    {
        using var db = new TestDatabase();
        await using (var c = db.Open())
        {
            c.Users.Add(new UserEntity { Name = "Ramesh", Password = "x" });
            await c.SaveChangesAsync(Ct);
        }

        var admin = new UserAdmin(db.Factory, new Inventory.Authentication.PasswordService());
        var id = (await admin.ListAsync()).Single(u => u.Name == "Ramesh").Id;
        Assert.False((await admin.SetPlacesAsync(id, [])).Ok);   // at least one
        Assert.True((await admin.SetPlacesAsync(id, [Inventory.Domain.Entities.Places.GodownId])).Ok);
        Assert.Equal([Inventory.Domain.Entities.Places.GodownId], (await admin.ListAsync()).Single(u => u.Id == id).PlaceIds);
        Assert.True((await admin.SetPlacesAsync(id, [Inventory.Domain.Entities.Places.StartingId, Inventory.Domain.Entities.Places.GodownId])).Ok);
        Assert.Empty((await admin.ListAsync()).Single(u => u.Id == id).PlaceIds);   // every place, also ones added later
        Assert.False((await admin.SetPlacesAsync(id, [99])).Ok);

        await using var d = db.Open();
        var texts = await d.ActivityLog.Where(a => a.RecordType == nameof(UserEntity)).Select(a => a.Text).ToListAsync(Ct);
        Assert.Contains("Place Godown given", texts);
        Assert.Contains("Place Godown taken away", texts);
    }

    [Fact]
    public void Selling_from_another_place_has_its_own_switch()
    {
        Assert.Contains(Permissions.All, p => p.Key == Permissions.SalesOtherPlace && p.Group == Permissions.StockGroup);
        var clerk = new UserAccess("Clerk", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.ScreenSales }, null)], DateTime.Now);
        Assert.False(clerk.Allows(Permissions.SalesOtherPlace));
        Assert.Null(clerk.Places);
    }
}
