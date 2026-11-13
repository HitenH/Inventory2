using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Models;
using Inventory.Service;

namespace Inventory.Tests;

/// <summary>The "Bulk Entry" pop-up on Purchase Orders, Purchases and Sales Orders (owner's request, 13 Nov 2026).</summary>
public class BulkEntryTests
{
    private static readonly DateOnly Day = new(2030, 3, 1);

    private static BulkEntryRow Row(Seed seed, Guid variant, string code, int qty, decimal? rate = 10, int? discount = null, string? remarks = null) => new()
    {
        ProductEntityId = seed.ProductId, ProductId = "P1", ProductName = "Product One",
        VariantEntityId = variant, VariantId = code, Quantity = qty, Rate = rate, Discount = discount, Remarks = remarks,
    };

    [Theory]
    [InlineData(false, true, 1, "Please select a product.")]
    [InlineData(true, false, 1, "Please select a variant.")]
    [InlineData(true, true, null, "Please enter a quantity.")]
    [InlineData(true, true, 0, "Please enter a quantity.")]
    [InlineData(true, true, 3, null)]
    public void A_row_needs_product_variant_and_quantity(bool hasProduct, bool hasVariant, int? qty, string? expected)
    {
        var message = BulkEntry.WhatIsMissing(hasProduct ? Guid.NewGuid() : Guid.Empty, hasVariant ? Guid.NewGuid() : null, qty);
        Assert.Equal(expected, message);
    }

    [Fact]
    public void Saving_needs_rows_and_for_purchase_orders_a_supplier()
    {
        var row = new BulkEntryRow { Quantity = 1 };
        Assert.Equal("Please select a supplier.", BulkEntry.WhatIsMissingToSave(BulkEntryKind.PurchaseOrder, new BulkOrderHeader(), [row]));
        Assert.Equal("There are no rows to save yet.", BulkEntry.WhatIsMissingToSave(BulkEntryKind.PurchaseOrder, new BulkOrderHeader { SupplierId = "S1" }, []));
        Assert.Null(BulkEntry.WhatIsMissingToSave(BulkEntryKind.PurchaseOrder, new BulkOrderHeader { SupplierId = "S1" }, [row]));
        // Voucher lines: the supplier/customer is already on the voucher.
        Assert.Null(BulkEntry.WhatIsMissingToSave(BulkEntryKind.Purchase, new BulkOrderHeader(), [row]));
    }

    [Fact]
    public void Lines_are_numbered_on_and_amounts_worked_out_like_the_line_form()
    {
        var seed = new Seed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var lines = BulkEntry.ToLines(
            [Row(seed, seed.Variant1Id, "V1", 3, rate: 100, discount: 10, remarks: "x"), Row(seed, seed.Variant1Id, "V1", 2, rate: 100)],
            firstSerialNumber: 4, withRemarks: false);

        Assert.Equal([4, 5], lines.Select(l => l.SerialNumber));
        Assert.Equal((300m, (decimal?)270m), (lines[0].Amount!.Value, lines[0].AmountAfterDiscount));
        Assert.Equal(200m, lines[1].AmountAfterDiscount);
        Assert.Null(lines[0].Remarks); // purchases have no remarks
        // Same variant twice stays two lines (owner's choice).
        Assert.Equal(2, lines.Count(l => l.VariantEntityId == seed.Variant1Id));
    }

    [Fact]
    public void Purchase_orders_all_get_the_supplier_dates_and_status()
    {
        var seed = new Seed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var header = new BulkOrderHeader { SupplierId = "S1", Date = Day, DueDate = Day.AddDays(7), OrderStatus = OrderStatus.Open };
        var orders = BulkEntry.ToPurchaseOrders([Row(seed, seed.Variant1Id, "V1", 5, remarks: "urgent"), Row(seed, seed.Variant2Id, "V2", 3)], header);

        Assert.All(orders, o => Assert.Equal(("S1", Day, Day.AddDays(7), OrderStatus.Open, "P1", "Product One"),
            (o.SupplierId, o.Date!.Value, o.DueDate!.Value, o.OrderStatus, o.ProductId, o.ProductName)));
        Assert.Equal(["V1", "V2"], orders.Select(o => o.VariantId));
        Assert.Equal([5, 3], orders.Select(o => o.Quantity!.Value));
        Assert.Equal("urgent", orders[0].Remarks);
    }

    [Fact]
    public async Task Bulk_purchase_orders_are_saved_together()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var repo = new PurchaseOrderRepositoryEF(db.Factory);
        var header = new BulkOrderHeader { SupplierId = "S1", Date = Day };

        await repo.CreateRange(BulkEntry.ToPurchaseOrders([Row(seed, seed.Variant1Id, "V1", 5), Row(seed, seed.Variant2Id, "V2", 3), Row(seed, seed.Variant1Id, "V1", 1)], header));

        var saved = await repo.GetAll();
        Assert.Equal(3, saved.Count);
        Assert.All(saved, o => Assert.Equal("S1", o.SupplierId));
    }

    [Fact]
    public async Task Status_of_ticked_purchase_orders_changes_in_one_go_and_only_for_those()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var repo = new PurchaseOrderRepositoryEF(db.Factory);
        await repo.CreateRange(BulkEntry.ToPurchaseOrders([Row(seed, seed.Variant1Id, "V1", 1), Row(seed, seed.Variant2Id, "V2", 2), Row(seed, seed.Variant1Id, "V1", 3)], new BulkOrderHeader { SupplierId = "S1" }));
        var all = await repo.GetAll();
        var ticked = all.Where(o => o.Quantity != 2).Select(o => o.Id).ToList();

        Assert.Equal(2, await repo.UpdateStatus(ticked, OrderStatus.Completed));

        var after = await repo.GetAll();
        Assert.All(after.Where(o => ticked.Contains(o.Id)), o => Assert.Equal(OrderStatus.Completed, o.OrderStatus));
        Assert.Equal(OrderStatus.Open, after.Single(o => o.Quantity == 2).OrderStatus);
        Assert.Equal(0, await repo.UpdateStatus([], OrderStatus.Partly));
    }

    [Fact]
    public async Task Bulk_purchase_lines_are_added_with_the_right_total_in_one_go()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lineRepo = new PurchaseVariantRepositoryEF(db.Factory);
        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await purchases.UpdateDiscount(id, 20);

        var lines = new Inventory.Shared.PurchaseLines(await purchases.GetById(id), lineRepo);
        await lines.CreateRange(BulkEntry.ToLines([Row(seed, seed.Variant1Id, "V1", 3, rate: 100), Row(seed, seed.Variant2Id, "V2", 2, rate: 50, discount: 10)], 1, withRemarks: false));

        await using var c = db.Open();
        Assert.Equal(2, c.PurchaseVariant.Count(l => l.PurchaseEntityId == id));
        // 300 + 90 - 20 discount on the voucher
        Assert.Equal(370, c.Purchases.Single(p => p.Id == id).TotalAmountProduct);
    }

    [Fact]
    public async Task One_bad_row_saves_none_of_the_rows()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lineRepo = new PurchaseVariantRepositoryEF(db.Factory);
        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });

        var good = Row(seed, seed.Variant1Id, "V1", 3);
        var bad = Row(seed, Guid.NewGuid(), "??", 1); // variant that does not exist: refused by the database rule
        var lines = new Inventory.Shared.PurchaseLines(await purchases.GetById(id), lineRepo);
        await Assert.ThrowsAnyAsync<Exception>(() => lines.CreateRange(BulkEntry.ToLines([good, bad], 1, withRemarks: false)));

        await using var c = db.Open();
        Assert.Equal(0, c.PurchaseVariant.Count(l => l.PurchaseEntityId == id));
    }

    [Fact]
    public async Task Bulk_sales_order_lines_keep_remarks_and_update_the_total()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var orders = new SalesOrderRepositoryEF(db.Factory);
        var lineRepo = new SalesOrderVariantRepositoryEF(db.Factory);
        var id = await orders.Create(new SalesOrderEntity { Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1 });

        var lines = new Inventory.Shared.SalesOrderLines(await orders.GetById(id), lineRepo);
        await lines.CreateRange(BulkEntry.ToLines([Row(seed, seed.Variant1Id, "V1", 2, rate: 10, remarks: "gift"), Row(seed, seed.Variant2Id, "V2", 1, rate: 30)], 1, withRemarks: true));

        await using var c = db.Open();
        var saved = c.SalesOrderVariants.Where(l => l.SalesOrderEntityId == id).OrderBy(l => l.SerialNumber).ToList();
        Assert.Equal(["gift", null], saved.Select(l => l.Remarks));
        Assert.Equal(50, c.SalesOrders.Single(o => o.Id == id).TotalAmountProduct);
    }
}
