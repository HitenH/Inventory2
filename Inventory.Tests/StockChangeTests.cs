using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 2, batch 2-1: stock = purchased − sold ± approved stock changes, split into sellable and damaged
/// ("way B": purchases and sales are never converted). Without stock changes the figures are exactly version 2.0's.
/// </summary>
public class StockChangeTests
{
    private static readonly DateOnly Day = new(2030, 2, 10);

    private static async Task PurchaseAsync(TestDatabase db, Seed seed, Guid variant, int qty)
    {
        await using var c = db.Open();
        c.Purchases.Add(new PurchaseEntity
        {
            Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1,
            PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = variant, SerialNumber = 1, Quantity = qty }],
        });
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SellAsync(TestDatabase db, Seed seed, Guid variant, int qty)
    {
        await using var c = db.Open();
        c.Sales.Add(new SalesEntity
        {
            Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1,
            SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = variant, Quantity = qty }],
        });
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<StockChangeEntity> ChangeAsync(TestDatabase db, Seed seed, StockChangeKind kind, StockChangeStatus status,
        params (Guid Variant, int Qty, StockState From, StockState To)[] lines)
    {
        await using var c = db.Open();
        var change = new StockChangeEntity
        {
            Kind = kind, Date = Day, VoucherId = 1, Status = status,
            CustomerEntityId = kind == StockChangeKind.SalesReturn ? seed.CustomerId : null,
            SupplierEntityId = kind == StockChangeKind.PurchaseReturn ? seed.SupplierId : null,
            Lines = lines.Select((l, i) => new StockChangeLineEntity
            {
                SerialNumber = i + 1, ProductEntityId = seed.ProductId, VariantEntityId = l.Variant, Quantity = l.Qty, From = l.From, To = l.To,
            }).ToList(),
        };
        c.StockChanges.Add(change);
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return change;
    }

    private static async Task<StockFigures> StockOf(TestDatabase db, Guid variant) =>
        (await new ProductRepositoryEF(db.Factory).GetStockByVariantIdsAsync([variant]))[variant];

    [Fact]
    public async Task Without_stock_changes_the_figures_are_purchased_minus_sold_and_nothing_damaged()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 10);
        await SellAsync(db, seed, seed.Variant1Id, 3);

        Assert.Equal(new StockFigures(7, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant2Id));   // no entries at all: 0, still in the answer
        Assert.False((await StockOf(db, seed.Variant1Id)).HasBreakdown);
    }

    [Fact]
    public async Task Approved_damage_moves_pieces_from_sellable_to_damaged()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 10);
        await ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Approved, (seed.Variant1Id, 1, StockState.Sellable, StockState.Damaged));

        var stock = await StockOf(db, seed.Variant1Id);
        Assert.Equal(new StockFigures(9, 1, 0), stock);
        Assert.True(stock.HasBreakdown);

        // Stock in hand (every screen of 2.0) now means the sellable pieces.
        var products = new ProductRepositoryEF(db.Factory);
        Assert.Equal(9, (await products.GetStockInHandByVariantIdsAsync([seed.Variant1Id]))[seed.Variant1Id]);
        var row = (await products.GetProductStockReportAsync()).Single(r => r.VariantId == "V1");
        Assert.Equal(9, row.StockInHand);
        Assert.Equal(1, row.Damaged);
        Assert.Equal(0, row.WaitingApproval);
    }

    [Fact]
    public async Task A_stock_change_waiting_for_approval_moves_nothing_and_a_rejected_one_is_ignored()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed, seed.Variant1Id, 10);
        await ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Waiting, (seed.Variant1Id, 2, StockState.Sellable, StockState.Damaged));
        await ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Rejected, (seed.Variant1Id, 5, StockState.Sellable, StockState.Damaged));

        var stock = await StockOf(db, seed.Variant1Id);
        Assert.Equal(new StockFigures(10, 0, 2), stock);
        Assert.True(stock.HasBreakdown);
        Assert.Equal(2, (await new ProductRepositoryEF(db.Factory).GetProductStockReportAsync()).Single(r => r.VariantId == "V1").WaitingApproval);
    }

    [Fact]
    public async Task Every_kind_of_stock_change_counts_the_right_way()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var v = seed.Variant1Id;
        await PurchaseAsync(db, seed, v, 20);                                                                                       // 20 sellable
        await SellAsync(db, seed, v, 5);                                                                                            // 15
        await ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Approved, (v, 4, StockState.Sellable, StockState.Damaged));          // 11 + 4 damaged
        await ChangeAsync(db, seed, StockChangeKind.Repaired, StockChangeStatus.Approved, (v, 1, StockState.Damaged, StockState.Sellable));        // 12 + 3
        await ChangeAsync(db, seed, StockChangeKind.WriteOff, StockChangeStatus.Approved, (v, 1, StockState.Damaged, StockState.Outside));         // 12 + 2
        await ChangeAsync(db, seed, StockChangeKind.PurchaseReturn, StockChangeStatus.Approved, (v, 2, StockState.Damaged, StockState.Outside));   // 12 + 0
        await ChangeAsync(db, seed, StockChangeKind.SalesReturn, StockChangeStatus.Approved,
            (v, 2, StockState.Outside, StockState.Sellable), (v, 1, StockState.Outside, StockState.Damaged));                                    // 14 + 1
        await ChangeAsync(db, seed, StockChangeKind.CountCorrection, StockChangeStatus.Approved, (v, 3, StockState.Outside, StockState.Sellable)); // 17 + 1
        await ChangeAsync(db, seed, StockChangeKind.CountCorrection, StockChangeStatus.Approved, (v, 1, StockState.Sellable, StockState.Outside)); // 16 + 1
        await ChangeAsync(db, seed, StockChangeKind.OpeningStock, StockChangeStatus.Approved, (v, 4, StockState.Outside, StockState.Sellable));    // 20 + 1
        await ChangeAsync(db, seed, StockChangeKind.PurchaseReturn, StockChangeStatus.Approved, (v, 3, StockState.Sellable, StockState.Outside));  // 17 + 1

        Assert.Equal(new StockFigures(17, 1, 0), await StockOf(db, v));
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant2Id));   // another variant is untouched
    }

    [Fact]
    public async Task The_reorder_report_compares_the_sellable_pieces_with_the_reorder_level()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();   // both variants have reorder level 5
        await PurchaseAsync(db, seed, seed.Variant1Id, 7);
        await PurchaseAsync(db, seed, seed.Variant2Id, 7);
        await ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Approved, (seed.Variant1Id, 3, StockState.Sellable, StockState.Damaged));

        var low = await new ProductRepositoryEF(db.Factory).GetProductStockReportAsync(onlyBelowReorderLevel: true);
        var row = Assert.Single(low);   // V1: 4 sellable ≤ 5; V2: 7 > 5
        Assert.Equal("V1", row.VariantId);
        Assert.Equal(4, row.StockInHand);
        Assert.Equal(3, row.Damaged);
    }

    [Theory]
    [InlineData(0, StockState.Sellable, StockState.Damaged)]
    [InlineData(-2, StockState.Sellable, StockState.Damaged)]
    [InlineData(1, StockState.Damaged, StockState.Damaged)]
    public async Task The_database_refuses_a_line_that_moves_nothing(int qty, StockState from, StockState to)
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Approved, (seed.Variant1Id, qty, from, to)));
        Assert.Contains("RULE: A stock change line must move more than zero pieces", ex.InnerException!.Message);
    }

    [Fact]
    public async Task The_database_refuses_a_line_whose_variant_is_not_of_its_product()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        Guid otherProduct;
        await using (var c = db.Open())
        {
            var p = new ProductEntity { ProductId = "P2", Name = "Product Two" };
            c.Products.Add(p);
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
            otherProduct = p.Id;
        }

        await using var context = db.Open();
        context.StockChanges.Add(new StockChangeEntity
        {
            Kind = StockChangeKind.Damage, Date = Day, VoucherId = 1, Status = StockChangeStatus.Approved,
            Lines = [new StockChangeLineEntity { SerialNumber = 1, ProductEntityId = otherProduct, VariantEntityId = seed.Variant1Id, Quantity = 1, From = StockState.Sellable, To = StockState.Damaged }],
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Contains("RULE: Each line must be a real product and one of its variants.", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Kinds_and_places_are_stored_as_words()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await ChangeAsync(db, seed, StockChangeKind.SalesReturn, StockChangeStatus.Waiting, (seed.Variant1Id, 1, StockState.Outside, StockState.Damaged));

        await using var connection = new SqliteConnection($"Data Source={db.DatabasePath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT c.Kind, c.Status, l.\"From\", l.\"To\" FROM StockChanges c JOIN StockChangeLines l ON l.StockChangeEntityId = c.Id";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.Equal(["SalesReturn", "Waiting", "Outside", "Damaged"], new[] { r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3) });
    }

    [Fact]
    public async Task A_stock_change_is_stamped_with_who_made_it_and_a_line_change_stamps_it_again()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        db.CurrentUser.Name = "Hiten";
        var change = await ChangeAsync(db, seed, StockChangeKind.Damage, StockChangeStatus.Approved, (seed.Variant1Id, 1, StockState.Sellable, StockState.Damaged));

        db.CurrentUser.Name = "Admin";
        await using (var c = db.Open())
        {
            var line = c.StockChangeLines.Single();
            line.ReadyToSend = true;
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = db.Open();
        var saved = check.StockChanges.Single(s => s.Id == change.Id);
        Assert.Equal("Hiten", saved.CreatedBy);
        Assert.Equal("Admin", saved.ChangedBy);
    }

    [Fact]
    public async Task Products_variants_customers_and_suppliers_with_stock_changes_cannot_be_deleted()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        Guid lonelySupplier;
        await using (var c = db.Open())
        {
            var s = new SupplierEntity { SupplierId = "S9", Name = "Repairs Ltd" };
            c.Suppliers.Add(s);
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
            lonelySupplier = s.Id;
        }

        await ChangeAsync(db, seed, StockChangeKind.SalesReturn, StockChangeStatus.Approved, (seed.Variant2Id, 1, StockState.Outside, StockState.Damaged));
        await using (var c = db.Open())
        {
            c.StockChangeLines.Single().ReturnSupplierEntityId = lonelySupplier;   // damaged lot to go back to "Repairs Ltd"
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var products = new ProductRepositoryEF(db.Factory);
        var variants = new VariantRepositoryEF(db.Factory);
        var customers = new CustomerRepositoryEF(db.Factory);
        var suppliers = new SupplierRepositoryEF(db.Factory);

        await Assert.ThrowsAnyAsync<Exception>(() => products.Delete(new ProductEntity { Id = seed.ProductId }));
        await Assert.ThrowsAnyAsync<Exception>(() => variants.Delete(new VariantEntity { Id = seed.Variant2Id, ProductEntityId = seed.ProductId }));
        await Assert.ThrowsAnyAsync<Exception>(() => customers.Delete(new CustomerEntity { Id = seed.CustomerId }));
        await Assert.ThrowsAnyAsync<Exception>(() => suppliers.Delete(new SupplierEntity { Id = lonelySupplier }));

        await using var check = db.Open();
        Assert.True(check.Products.Any(p => p.Id == seed.ProductId));
        Assert.True(check.Variants.Any(v => v.Id == seed.Variant2Id));
        Assert.True(check.Customers.Any(c => c.Id == seed.CustomerId));
        Assert.True(check.Suppliers.Any(s => s.Id == lonelySupplier));
    }

    [Fact]
    public async Task The_starting_reasons_come_with_the_upgrade()
    {
        using var db = new TestDatabase();
        await using var c = db.Open();
        var reasons = await c.StockChangeReasons.OrderBy(r => r.SortOrder).Select(r => r.Name).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(StockReasons.Starting, reasons);
        Assert.Contains("Other", reasons);
    }

    [Theory]
    [InlineData(13, 3, 0, "+ 3 damaged", true)]
    [InlineData(13, 3, 2, "+ 3 damaged · 2 waiting", true)]
    [InlineData(10, 0, 2, "2 waiting", true)]
    [InlineData(10, 0, 0, "", false)]
    public void The_stock_figure_names_what_is_counted_beside_the_sellable_pieces(int sellable, int damaged, int waiting, string text, bool breakdown)
    {
        var figures = new StockFigures(sellable, damaged, waiting);
        Assert.Equal(text, Inventory.Shared.StockFigure.Describe(figures));
        Assert.Equal(breakdown, figures.HasBreakdown);
    }

    [Theory]
    [InlineData("PurchaseVariant")]
    [InlineData("SalesVariants")]
    public void Stock_is_added_up_from_the_index_without_reading_every_line(string table)
    {
        using var db = new TestDatabase();
        using var connection = new SqliteConnection($"Data Source={db.DatabasePath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"EXPLAIN QUERY PLAN SELECT VariantEntityId, SUM(Quantity) FROM \"{table}\" GROUP BY VariantEntityId";
        using var r = cmd.ExecuteReader();
        var plan = new List<string>();
        while (r.Read())
        {
            plan.Add(r.GetString(3));
        }

        Assert.Contains(plan, step => step.Contains($"COVERING INDEX IX_{table}_VariantEntityId_Quantity"));
    }
}
