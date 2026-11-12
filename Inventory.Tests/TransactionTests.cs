using Inventory.Domain.Entities;
using Inventory.Domain.Repository;

namespace Inventory.Tests;

/// <summary>Batch 7: totals always up to date, discounts kept, sales never emptied.</summary>
public class TransactionTests
{
    private static readonly DateOnly Day = new(2030, 2, 1);

    private static async Task<decimal?> PurchaseTotal(TestDatabase db, Guid id)
    {
        await using var c = db.Open();
        return c.Purchases.Single(p => p.Id == id).TotalAmountProduct;
    }

    [Fact]
    public async Task Purchase_total_follows_every_line_change_and_the_discount()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lines = new PurchaseVariantRepositoryEF(db.Factory);
        var suppliers = new SupplierRepositoryEF(db.Factory);

        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 1, AmountAfterDiscount = 100 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, SerialNumber = 2, Quantity = 1, AmountAfterDiscount = 50 });
        Assert.Equal(150, await PurchaseTotal(db, id));

        await purchases.UpdateDiscount(id, 20);
        Assert.Equal(130, await PurchaseTotal(db, id));

        // Edit a line: the discount stays, the total follows.
        var line = (await purchases.GetById(id)).PurchaseVariants!.Single(l => l.SerialNumber == 2);
        line.AmountAfterDiscount = 80;
        await lines.Update(line);
        Assert.Equal(160, await PurchaseTotal(db, id));

        // Delete a line.
        await lines.Delete(line);
        Assert.Equal(80, await PurchaseTotal(db, id));

        // The supplier total on the Suppliers page moves immediately (no "Save Total Amount" needed).
        Assert.Equal(80, (await suppliers.GetPurchaseTotalsBySupplierAsync())[seed.SupplierId]);

        // Saving the header (Update button) keeps the discount and recalculates.
        var header = await purchases.GetById(id);
        header.Remarks = "checked";
        await purchases.Update(header);
        Assert.Equal(80, await PurchaseTotal(db, id));
        await using var c = db.Open();
        Assert.Equal(20, c.Purchases.Single(p => p.Id == id).Discount);
    }

    [Fact]
    public async Task Discount_and_line_saved_at_the_same_moment_give_the_right_total()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lines = new PurchaseVariantRepositoryEF(db.Factory);
        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });

        for (var i = 0; i < 15; i++)
        {
            var discount = i + 1;
            var serial = i + 1;
            await Task.WhenAll(
                Task.Run(() => purchases.UpdateDiscount(id, discount), TestContext.Current.CancellationToken),
                Task.Run(() => lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = serial, Quantity = 1, AmountAfterDiscount = 10 }), TestContext.Current.CancellationToken));
        }

        Assert.Equal(150 - 15, await PurchaseTotal(db, id));
    }

    [Fact]
    public async Task Sales_order_discount_is_stored_and_kept_when_lines_are_added()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var orders = new SalesOrderRepositoryEF(db.Factory);
        var lines = new SalesOrderVariantRepositoryEF(db.Factory);

        // A brand-new order must save (batch 7 bug: it tried to insert an empty customer).
        var id = await orders.Create(new SalesOrderEntity { Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1 });
        await lines.Create(new SalesOrderVariantEntity { SalesOrderEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 1, AmountAfterDiscount = 200 });
        await orders.UpdateDiscount(id, 25);
        await lines.Create(new SalesOrderVariantEntity { SalesOrderEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, SerialNumber = 2, Quantity = 1, AmountAfterDiscount = 100 });

        await using var c = db.Open();
        var order = c.SalesOrders.Single(o => o.Id == id);
        Assert.Equal(25, order.Discount);
        Assert.Equal(275, order.TotalAmountProduct);
        Assert.Equal(1, c.Customers.Count());   // no empty customer was created
    }

    [Fact]
    public async Task A_sale_cannot_be_emptied_and_a_summary_save_keeps_the_items()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var sales = new SaleRepositoryEF(db.Factory);
        var id = await sales.Create(new SalesEntity
        {
            Date = Day,
            CustomerEntityId = seed.CustomerId,
            VoucherId = 1,
            SalesVariants = new() { new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2 } }
        });

        var emptied = await sales.GetById(id);
        emptied.SalesVariants = new();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sales.Update(emptied));
        Assert.Equal(SaleRepositoryEF.EmptySaleMessage, ex.Message);
        Assert.Single((await sales.GetById(id)).SalesVariants!);

        // Summary screen save: rates, discounts, remarks; items untouched (null list).
        var summary = await sales.GetById(id);
        summary.SalesSummaries = new()
        {
            new SalesSummaryEntity { ProductEntityId = seed.ProductId, SerialNumber = 1, ProductId = "P1", ProductName = "Product One", Quantity = 2, ProductRate = 40, Amount = 80, Discount = 10, AmountAfterDiscount = 72 }
        };
        summary.Discount = 2;
        summary.TotalAmountProduct = 70;
        summary.Remarks = "paid cash";
        summary.SalesVariants = null;
        await sales.Update(summary);

        var saved = await sales.GetById(id);
        Assert.Equal(40, saved.SalesSummaries!.Single().ProductRate);
        Assert.Equal(2, saved.Discount);
        Assert.Equal("paid cash", saved.Remarks);
        Assert.Equal(2, saved.SalesVariants!.Single().Quantity);
    }
}
