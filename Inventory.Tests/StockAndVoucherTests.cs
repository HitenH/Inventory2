using Inventory.Domain.Entities;
using Inventory.Domain.Repository;

namespace Inventory.Tests;

/// <summary>Stock = purchased minus sold per variant; voucher numbers per day.</summary>
public class StockAndVoucherTests
{
    private static readonly DateOnly Day = new(2030, 1, 15);

    private static SalesEntity Sale(Seed seed, int voucher, params (Guid variant, int qty)[] items) => new()
    {
        Date = Day,
        CustomerEntityId = seed.CustomerId,
        VoucherId = voucher,
        SalesVariants = items.Select(i => new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = i.variant, Quantity = i.qty }).ToList()
    };

    [Fact]
    public async Task Stock_is_purchased_minus_sold_per_variant()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lines = new PurchaseVariantRepositoryEF(db.Factory);
        var sales = new SaleRepositoryEF(db.Factory);
        var products = new ProductRepositoryEF(db.Factory);

        var purchaseId = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 10 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, SerialNumber = 2, Quantity = 4 });
        await sales.Create(Sale(seed, 1, (seed.Variant1Id, 3), (seed.Variant1Id, 2)));

        var stock = await products.GetStockInHandByVariantIdsAsync(new[] { seed.Variant1Id, seed.Variant2Id });
        Assert.Equal(5, stock[seed.Variant1Id]);
        Assert.Equal(4, stock[seed.Variant2Id]);

        // The stock report must agree with the per-variant figure.
        var report = await products.GetProductStockReportAsync();
        Assert.Equal(5, report.Single(r => r.VariantId == "V1").StockInHand);
        Assert.Equal(4, report.Single(r => r.VariantId == "V2").StockInHand);
        Assert.Equal(5, report.Single(r => r.VariantId == "V1").SalesQuantity);

        // At or below the reorder level (5): V1 (5) and V2 (4) both show.
        var low = await products.GetProductStockReportAsync(onlyBelowReorderLevel: true);
        Assert.Equal(2, low.Count);
    }

    [Fact]
    public async Task Deleting_a_sale_restores_stock()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var sales = new SaleRepositoryEF(db.Factory);
        var products = new ProductRepositoryEF(db.Factory);

        var saleId = await sales.Create(Sale(seed, 1, (seed.Variant1Id, 7)));
        Assert.Equal(-7, (await products.GetStockInHandByVariantIdsAsync(new[] { seed.Variant1Id }))[seed.Variant1Id]);

        await sales.Delete(await sales.GetById(saleId));
        Assert.Equal(0, (await products.GetStockInHandByVariantIdsAsync(new[] { seed.Variant1Id }))[seed.Variant1Id]);
    }

    [Fact]
    public async Task Voucher_numbers_count_per_day()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var sales = new SaleRepositoryEF(db.Factory);

        Assert.Equal(0, await purchases.GetLastVoucherIdByDate(Day));
        await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 2 });
        await purchases.Create(new PurchaseEntity { Date = Day.AddDays(1), SupplierEntityId = seed.SupplierId, VoucherId = 9 });

        Assert.Equal(2, await purchases.GetLastVoucherIdByDate(Day));
        Assert.True(await purchases.IsVoucherExistByDate(2, Day));
        Assert.False(await purchases.IsVoucherExistByDate(3, Day));
        Assert.False(await purchases.IsVoucherExistByDate(2, Day.AddDays(1)));

        Assert.Equal(0, sales.GetLastVoucherIdByDate(Day));
        await sales.Create(Sale(seed, 1, (seed.Variant1Id, 1)));
        Assert.Equal(1, sales.GetLastVoucherIdByDate(Day));
        Assert.True(sales.IsVoucherExistByDate(1, Day));
    }
}
