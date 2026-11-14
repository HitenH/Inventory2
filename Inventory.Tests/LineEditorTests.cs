using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Models;
using Inventory.Shared;

namespace Inventory.Tests;

/// <summary>Batch 12: the shared Purchase / Sales Order line editor saves lines through one small class per voucher type.</summary>
public class LineEditorTests
{
    private static readonly DateOnly Day = new(2030, 3, 1);

    private static LineEditorModel NewLine(Guid variantId, int qty, decimal rate, string? remarks = null) => new()
    {
        VariantEntityId = variantId, Quantity = qty, ProductRate = rate,
        Amount = qty * rate, DiscountPercent = 0, AmountAfterDiscount = qty * rate, Remarks = remarks,
    };

    [Fact]
    public async Task Purchase_lines_add_edit_delete_and_total_follows()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });

        var lines = new PurchaseLines(await purchases.GetById(id), new PurchaseVariantRepositoryEF(db.Factory));
        Assert.False(lines.HasRemarks);
        await lines.Create(NewLine(seed.Variant1Id, 2, 10), seed.ProductId, 2);
        await lines.Create(NewLine(seed.Variant2Id, 1, 5), seed.ProductId, 1);

        // Reload as the page does: lines come back in serial-number order with product details.
        lines = new PurchaseLines(await purchases.GetById(id), new PurchaseVariantRepositoryEF(db.Factory));
        var loaded = lines.Load();
        Assert.Equal(new[] { 1, 2 }, loaded.Select(l => l.SerialNumber));
        Assert.Equal(seed.Variant2Id, loaded[0].VariantEntityId);
        Assert.False(string.IsNullOrEmpty(loaded[0].ProductId));
        Assert.Equal(25, (await purchases.GetById(id)).TotalAmountProduct);

        loaded[1].Quantity = 3; loaded[1].Amount = 30; loaded[1].AmountAfterDiscount = 30;
        await lines.Update(loaded[1], seed.ProductId);
        Assert.Equal(35, (await purchases.GetById(id)).TotalAmountProduct);

        await lines.Delete(loaded[0].Id);
        var after = await purchases.GetById(id);
        Assert.Equal(30, after.TotalAmountProduct);
        Assert.Equal(3, Assert.Single(after.PurchaseVariants!).Quantity);
    }

    [Fact]
    public async Task Sales_order_lines_keep_remarks_and_total_follows()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var orders = new SalesOrderRepositoryEF(db.Factory);
        var id = await orders.Create(new SalesOrderEntity { Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1 });

        var lines = new SalesOrderLines(await orders.GetById(id), new SalesOrderVariantRepositoryEF(db.Factory));
        Assert.True(lines.HasRemarks);
        await lines.Create(NewLine(seed.Variant1Id, 4, 2.5m, "urgent"), seed.ProductId, 1);

        lines = new SalesOrderLines(await orders.GetById(id), new SalesOrderVariantRepositoryEF(db.Factory));
        var line = Assert.Single(lines.Load());
        Assert.Equal("urgent", line.Remarks);
        Assert.Equal(10, (await orders.GetById(id)).TotalAmountProduct);

        line.Remarks = "changed"; line.Quantity = 2; line.Amount = 5; line.AmountAfterDiscount = 5;
        await lines.Update(line, seed.ProductId);
        var saved = Assert.Single((await orders.GetById(id)).SalesOrderVariants!);
        Assert.Equal(("changed", (int?)2), (saved.Remarks, saved.Quantity));
        Assert.Equal(5, (await orders.GetById(id)).TotalAmountProduct);

        await lines.Delete(line.Id);
        Assert.Empty((await orders.GetById(id)).SalesOrderVariants!);
        await lines.Delete(Guid.NewGuid()); // a line that is already gone: nothing happens
    }
}
