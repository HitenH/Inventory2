using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Inventory.Service.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.Tests;

/// <summary>Batch 8: importing Purchases (opening stock). Row rules without a database, then the service on a temporary database.</summary>
public class ImportPurchaseTests
{
    private const string Header = "Date,Supplier ID,Product ID,Variant ID,Quantity,Rate,Discount %,Remarks\n";

    // ---- helpers for the rule tests -------------------------------------------------------------------------

    private static readonly string[] Suppliers = ["S1", "S2"];
    private static readonly KnownProduct[] Products =
    [
        new("P1", ["V1", "V2"]),
        new("P2", ["G"]),
    ];

    private static IReadOnlyList<TemplateRow> Rows(string body)
    {
        var mapped = ImportChecker.MapHeadings(ImportKind.Purchases, ImportFileReader.Read(ImportTests.Bytes(Header + body), "x.csv"));
        Assert.Empty(mapped.FileIssues);
        return mapped.Rows;
    }

    private static List<ImportPreviewRow> Check(string body, ExistingPurchase[]? existing = null) =>
        ImportChecker.CheckPurchases(Rows(body), Suppliers, Products, existing ?? []);

    private static string[] Messages(IEnumerable<ImportPreviewRow> rows) => rows.SelectMany(r => r.Issues).Select(i => i.Message).ToArray();

    private static string Only(IEnumerable<ImportPreviewRow> rows) => Assert.Single(Messages(rows));

    // ---- template and headings ------------------------------------------------------------------------------

    [Fact]
    public void Purchases_template_has_the_agreed_columns()
    {
        var columns = ImportTemplates.Columns(ImportKind.Purchases);
        Assert.Equal(["Date", "Supplier ID", "Product ID", "Variant ID", "Quantity", "Rate", "Discount %", "Remarks"], columns.Select(c => c.Header));
        Assert.Equal([true, true, true, true, true, false, false, false], columns.Select(c => c.Required));
        Assert.Equal("purchase", ImportTemplates.Noun(ImportKind.Purchases));
        Assert.Equal("purchases", ImportTemplates.Noun(ImportKind.Purchases, 2));
        Assert.Equal("purchases-import-sample.csv", ImportTemplates.SampleFileName(ImportKind.Purchases));
        Assert.All(columns, c => Assert.False(string.IsNullOrWhiteSpace(c.Hint)));
    }

    [Theory]
    [InlineData("Discount %")]
    [InlineData("Discount")]
    [InlineData("discount%")]
    [InlineData(" DISCOUNT % ")]
    [InlineData("Discount_%")]
    public void Discount_heading_matches_with_or_without_the_percent_sign(string heading)
    {
        var csv = $"Date,Supplier ID,Product ID,Variant ID,Quantity,{heading}\n01-04-2026,S1,P1,V1,3,10\n";
        var mapped = ImportChecker.MapHeadings(ImportKind.Purchases, ImportFileReader.Read(ImportTests.Bytes(csv), "x.csv"));
        Assert.Empty(mapped.FileIssues);
        Assert.Equal("10", mapped.Rows[0].Cells[6]);
    }

    [Fact]
    public void Required_headings_are_checked_and_optional_ones_may_be_left_out()
    {
        var ok = ImportChecker.MapHeadings(ImportKind.Purchases,
            ImportFileReader.Read(ImportTests.Bytes("Date,Supplier ID,Product ID,Variant ID,Quantity\n01-04-2026,S1,P1,V1,3\n"), "x.csv"));
        Assert.Empty(ok.FileIssues);

        var missing = ImportChecker.MapHeadings(ImportKind.Purchases,
            ImportFileReader.Read(ImportTests.Bytes("Date,Supplier ID,Product ID,Quantity\n01-04-2026,S1,P1,3\n"), "x.csv"));
        Assert.Contains(missing.FileIssues, i => i.Message == "Column 'Variant ID' is missing.");
    }

    // ---- dates ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("01-04-2026")]
    [InlineData("01/04/2026")]
    [InlineData("1-4-2026")]
    [InlineData("1/4/2026")]
    [InlineData("2026-04-01")]
    public void Accepted_date_forms_all_mean_the_first_of_april(string text)
    {
        Assert.Equal(new DateOnly(2026, 4, 1), ImportChecker.ParseDate(text));
        Assert.Empty(Messages(Check($"{text},S1,P1,V1,3,,,\n")));
    }

    [Fact]
    public void Day_comes_first_so_13_in_the_middle_is_not_a_date()
    {
        Assert.Equal(new DateOnly(2026, 1, 4), ImportChecker.ParseDate("04-01-2026"));   // 4 January, never 1 April
        Assert.Null(ImportChecker.ParseDate("04-13-2026"));
        Assert.Null(ImportChecker.ParseDate("2026-13-01"));
    }

    [Theory]
    [InlineData("April 1 2026")]
    [InlineData("1-4-26")]
    [InlineData("2026/04/01")]
    [InlineData("31-02-2026")]
    [InlineData("01-04-0026")]
    [InlineData("01.04.2026")]
    [InlineData("tomorrow")]
    public void Other_date_forms_are_refused_with_the_right_form_shown(string text)
    {
        Assert.Null(ImportChecker.ParseDate(text));
        Assert.Equal($"Date '{text}' is not a valid date. Use day-month-year, like 01-04-2026.", Only(Check($"{text},S1,P1,V1,3,,,\n")));
    }

    [Fact]
    public void Date_is_required()
    {
        Assert.Equal("Date is required.", Only(Check(",S1,P1,V1,3,,,\n")));
    }

    // ---- supplier, product, variant -------------------------------------------------------------------------

    [Fact]
    public void Supplier_must_exist_ignoring_case()
    {
        Assert.Empty(Messages(Check("01-04-2026,s1,p1,v1,3,,,\n")));
        Assert.Equal("Supplier ID 'S9' does not exist. Add the supplier first.", Only(Check("01-04-2026,S9,P1,V1,3,,,\n")));
        Assert.Equal("Supplier ID is required.", Only(Check("01-04-2026,,P1,V1,3,,,\n")));
    }

    [Fact]
    public void Product_must_exist_and_variant_must_belong_to_that_product()
    {
        Assert.Equal("Product ID 'PX' does not exist. Add the product first.", Only(Check("01-04-2026,S1,PX,V1,3,,,\n")));
        Assert.Equal("Variant ID 'G' does not exist for product 'P1'.", Only(Check("01-04-2026,S1,P1,G,3,,,\n")));   // G belongs to P2
        Assert.Empty(Messages(Check("01-04-2026,S1,P2,g,3,,,\n")));
        Assert.Equal("Product ID is required.", Only(Check("01-04-2026,S1,,V1,3,,,\n")));
        Assert.Equal("Variant ID is required.", Only(Check("01-04-2026,S1,P1,,3,,,\n")));
    }

    [Fact]
    public void An_unknown_product_gives_one_message_not_two()
    {
        // The variant cannot be judged when the product is unknown, so only the product is reported.
        Assert.Equal("Product ID 'PX' does not exist. Add the product first.", Only(Check("01-04-2026,S1,PX,ZZ,3,,,\n")));
    }

    // ---- quantity, rate, discount ---------------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("2.5")]
    [InlineData("abc")]
    [InlineData("1,000")]
    [InlineData("99999999999")]
    public void Quantity_must_be_a_whole_number_above_zero(string quantity)
    {
        Assert.Equal($"Quantity '{quantity}' must be a whole number greater than 0.", Only(Check($"01-04-2026,S1,P1,V1,\"{quantity}\",,,\n")));
    }

    [Fact]
    public void Quantity_is_required_and_large_whole_numbers_are_fine()
    {
        Assert.Equal("Quantity is required.", Only(Check("01-04-2026,S1,P1,V1,,,,\n")));
        Assert.Empty(Messages(Check("01-04-2026,S1,P1,V1,1,,,\n")));
        Assert.Empty(Messages(Check("01-04-2026,S1,P1,V1,100000,,,\n")));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("250", true)]
    [InlineData("1250.50", true)]
    [InlineData(".5", true)]
    [InlineData("0", true)]
    [InlineData("-5", false)]
    [InlineData("1,250", false)]
    [InlineData("Rs 250", false)]
    public void Rate_follows_the_product_import_rules(string rate, bool valid)
    {
        var messages = Messages(Check($"01-04-2026,S1,P1,V1,3,\"{rate}\",,\n"));
        if (valid)
            Assert.Empty(messages);
        else
            Assert.Contains("Rate '", Assert.Single(messages));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("10", true)]
    [InlineData("100", true)]
    [InlineData("101", false)]
    [InlineData("-1", false)]
    [InlineData("12.5", false)]
    [InlineData("ten", false)]
    public void Discount_is_a_whole_number_from_0_to_100(string discount, bool valid)
    {
        var messages = Messages(Check($"01-04-2026,S1,P1,V1,3,,\"{discount}\",\n"));
        if (valid)
            Assert.Empty(messages);
        else
            Assert.Equal($"Discount % '{discount}' must be a whole number from 0 to 100.", Assert.Single(messages));
    }

    // ---- one purchase = same Date + Supplier ----------------------------------------------------------------

    [Fact]
    public void Remarks_must_be_the_same_on_every_row_of_one_purchase_ignoring_case()
    {
        Assert.Empty(Messages(Check("01-04-2026,S1,P1,V1,3,,,Opening stock\n01-04-2026,s1,P1,V2,3,,,OPENING STOCK\n")));

        var rows = Check("01-04-2026,S1,P1,V1,3,,,Opening stock\n01-04-2026,S1,P1,V2,3,,,Other\n");
        Assert.Empty(rows[0].Issues);
        Assert.Equal("Remarks differs from row 2 ('Opening stock'). All rows of the purchase from supplier 'S1' dated 01-04-2026 must have the same Remarks.",
            Assert.Single(rows[1].Issues).Message);
    }

    [Fact]
    public void Different_remarks_on_different_purchases_are_fine()
    {
        Assert.Empty(Messages(Check("01-04-2026,S1,P1,V1,3,,,A\n02-04-2026,S1,P1,V1,3,,,B\n01-04-2026,S2,P1,V1,3,,,C\n")));
    }

    [Fact]
    public void The_same_product_and_variant_twice_in_one_purchase_is_flagged_on_every_row()
    {
        var rows = Check("01-04-2026,S1,P1,V1,3,,,\n01-04-2026,S1,P1,V2,3,,,\n01-04-2026,S1,p1,v1,2,,,\n");
        Assert.Equal("Product 'P1' with Variant ID 'V1' is also on row 4 of the same purchase. Add the quantities together on one row.",
            Assert.Single(rows[0].Issues).Message);
        Assert.Empty(rows[1].Issues);
        Assert.Equal("Product 'p1' with Variant ID 'v1' is also on row 2 of the same purchase. Add the quantities together on one row.",
            Assert.Single(rows[2].Issues).Message);
    }

    [Fact]
    public void The_same_variant_in_two_different_purchases_is_fine()
    {
        Assert.Empty(Messages(Check("01-04-2026,S1,P1,V1,3,,,\n02-04-2026,S1,P1,V1,3,,,\n01-04-2026,S2,P1,V1,3,,,\n")));
    }

    [Fact]
    public void Dates_in_different_spellings_still_make_one_purchase()
    {
        // 01-04-2026 and 2026-04-01 are the same day, so the same variant twice is a duplicate.
        var rows = Check("01-04-2026,S1,P1,V1,3,,,\n2026-04-01,S1,P1,V1,3,,,\n");
        Assert.All(rows, r => Assert.Contains("of the same purchase", Assert.Single(r.Issues).Message));
    }

    // ---- double-import guard --------------------------------------------------------------------------------

    [Fact]
    public void A_purchase_that_already_exists_is_refused_on_every_row_of_it()
    {
        // Voucher 3 holds exactly the two items of the file's first purchase (order and case differ).
        ExistingPurchase[] existing = [new("s1", new DateOnly(2026, 4, 1), 3, [new("p1", "v2", 3), new("P1", "V1", 3)])];
        var rows = Check("01-04-2026,S1,P1,V1,3,,,\n01-04-2026,S1,P1,V2,3,,,\n01-04-2026,S2,P1,V1,3,,,\n02-04-2026,S1,P1,V1,3,,,\n", existing);

        const string expected = "A purchase from supplier 'S1' dated 01-04-2026 with exactly the same items and quantities already exists (voucher 3). Importing it again would count the stock twice.";
        Assert.Equal(expected, Assert.Single(rows[0].Issues).Message);
        Assert.Equal(expected, Assert.Single(rows[1].Issues).Message);
        Assert.Empty(rows[2].Issues);   // other supplier, same date
        Assert.Empty(rows[3].Issues);   // same supplier, other date
    }

    [Fact]
    public void Another_purchase_from_the_same_supplier_on_the_same_date_is_allowed()
    {
        // Owner, 14 Nov 2026: same supplier and date is fine; only an exact copy (same items and quantities) is stopped.
        var day = new DateOnly(2026, 4, 1);
        ExistingPurchase[] existing =
        [
            new("S1", day, 1, [new("P1", "V1", 5)]),                       // other quantity
            new("S1", day, 2, [new("P1", "V2", 3)]),                       // other variant
            new("S1", day, 3, [new("P1", "V1", 3), new("P1", "V2", 1)]),   // one item more
            new("S1", day, 4, []),                                         // no items
        ];
        Assert.Empty(Messages(Check("01-04-2026,S1,P1,V1,3,,,\n", existing)));
    }

    [Fact]
    public void Several_existing_vouchers_are_all_named()
    {
        ExistingPurchase[] existing = [new("S1", new DateOnly(2026, 4, 1), 4, [new("P1", "V1", 3)]), new("S1", new DateOnly(2026, 4, 1), 2, [new("P1", "V1", 3)])];
        Assert.Equal("A purchase from supplier 'S1' dated 01-04-2026 with exactly the same items and quantities already exists (vouchers 2, 4). Importing it again would count the stock twice.",
            Only(Check("01-04-2026,S1,P1,V1,3,,,\n", existing)));
    }

    [Fact]
    public void Preview_cells_show_the_date_as_typed()
    {
        var rows = Check("1/4/2026,S1,P1,V1,3,,,\n");
        Assert.Equal("1/4/2026", rows[0].Cells[0]);
    }

    [Fact]
    public void Build_makes_one_purchase_per_date_and_supplier_in_file_order_with_defaults()
    {
        var drafts = ImportChecker.BuildPurchases(Rows(
            "02-04-2026,S1,P1,V1,3,,,A\n01-04-2026,S2,P1,V1,1,12.5,10,\n02-04-2026,s1,P1,V2,2,100,0,A\n"));
        Assert.Equal(2, drafts.Count);
        Assert.Equal(new DateOnly(2026, 4, 2), drafts[0].Date);
        Assert.Equal("A", drafts[0].Remarks);
        Assert.Equal(2, drafts[0].Lines.Count);
        Assert.Equal(new PurchaseLineDraft("P1", "V1", 3, 0m, 0), drafts[0].Lines[0]);   // empty Rate = 0, empty Discount = 0
        Assert.Null(drafts[1].Remarks);
        Assert.Equal(new PurchaseLineDraft("P1", "V1", 1, 12.5m, 10), drafts[1].Lines[0]);
    }

    // ---- the service on a real temporary database -----------------------------------------------------------

    private static MasterImportService Service(TestDatabase db, BackupService? backup = null) =>
        new(db.Factory, NullLogger<MasterImportService>.Instance, new ConfigurationBuilder().Build(), backup);

    private static Task<ImportPreview> Preview(MasterImportService service, string csv, string name = "purchases.csv") =>
        service.PreviewAsync(ImportKind.Purchases, ImportTests.Bytes(csv), name);

    /// <summary>Supplier S001; product P001 with variants G and L (what the sample file uses).</summary>
    private static async Task SeedSampleAsync(TestDatabase db)
    {
        await using var context = db.Open();
        var product = new ProductEntity { ProductId = "P001", Name = "LED Frame", Rate = 250m };
        context.AddRange(
            new SupplierEntity { SupplierId = "S001", Name = "Gupta" },
            product,
            new VariantEntity { VariantId = "G", Name = "GANESH", Product = product },
            new VariantEntity { VariantId = "L", Name = "LAXMI", Product = product });
        await context.SaveChangesAsync();
    }

    private static async Task<(Guid G, Guid L)> SampleVariantsAsync(TestDatabase db)
    {
        await using var context = db.Open();
        var variants = await context.Variants.ToListAsync();
        return (variants.Single(v => v.VariantId == "G").Id, variants.Single(v => v.VariantId == "L").Id);
    }

    private static BackupService Backups(TestDatabase db, string? backupFolder = null)
    {
        var paths = DataPaths.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataFolder"] = db.Folder,
            ["BackupFolder"] = backupFolder ?? Path.Combine(db.Folder, "Backups"),
        }).Build());
        return new BackupService(paths, NullLogger<BackupService>.Instance);
    }

    [Fact]
    public async Task Sample_file_previews_clean_imports_and_a_second_import_is_refused()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var service = Service(db);
        var sample = ImportTemplates.SampleCsv(ImportKind.Purchases);

        var preview = await Preview(service, sample, ImportTemplates.SampleFileName(ImportKind.Purchases));
        Assert.Empty(preview.FileIssues);
        Assert.Equal(0, preview.IssueCount);
        Assert.True(preview.CanImport);
        Assert.Equal(1, preview.RecordCount);   // two rows, one purchase
        Assert.Equal(2, preview.LineCount);

        Assert.Equal(1, await service.ImportAsync(preview));

        await using (var context = db.Open())
        {
            var purchase = await context.Purchases.Include(p => p.PurchaseVariants).SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new DateOnly(2026, 4, 1), purchase.Date);
            Assert.Equal(1, purchase.VoucherId);
            Assert.Equal("Opening stock", purchase.Remarks);
            Assert.Equal(2, purchase.PurchaseVariants!.Count);
        }

        // The same preview cannot be saved again, and previewing the same file again is stopped by the guard on every row.
        var again = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Contains("already exists (voucher 1)", again.Message);
        Assert.EndsWith("Nothing was imported.", again.Message);

        var second = await Preview(service, sample);
        Assert.False(second.CanImport);
        Assert.Equal(2, second.Rows.Count(r => r.Issues.Any(i => i.Message.Contains("Importing it again would count the stock twice"))));
        Assert.Equal(1, await db.Open().Purchases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.Open().PurchaseVariant.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Lines_amounts_total_and_header_are_what_the_purchase_screen_would_save()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var service = Service(db);

        var preview = await Preview(service,
            Header + "01-04-2026,S001,P001,G,10,250,0,Note\n01-04-2026,S001,P001,L,5,99.99,10,Note\n");
        Assert.True(preview.CanImport, string.Join("; ", Messages(preview.Rows)));
        await service.ImportAsync(preview);

        await using var context = db.Open();
        var purchase = await context.Purchases.Include(p => p.PurchaseVariants).SingleAsync(TestContext.Current.CancellationToken);
        var lines = purchase.PurchaseVariants!.OrderBy(l => l.SerialNumber).ToList();

        Assert.Equal([1, 2], lines.Select(l => l.SerialNumber));
        Assert.Equal(10, lines[0].Quantity);
        Assert.Equal(2500m, lines[0].Amount);
        Assert.Equal(0, lines[0].DiscountPercent);
        Assert.Equal(LineAmounts.ComputeAmountAfterDiscount(2500m, 0), lines[0].AmountAfterDiscount);
        Assert.Equal(499.95m, lines[1].Amount);
        Assert.Equal(10, lines[1].DiscountPercent);
        Assert.Equal(449.96m, lines[1].AmountAfterDiscount);
        Assert.Equal(0m, purchase.DiscountAmount);
        Assert.Equal(2500m + 449.96m, purchase.TotalAmountProduct);

        // Same number as the screen's own recalculation.
        var stored = purchase.TotalAmountProduct;
        purchase.TotalAmountProduct = null;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await TransactionTotals.RecalculatePurchaseAsync(context, purchase.Id);
        Assert.Equal(stored, purchase.TotalAmountProduct);
    }

    [Fact]
    public async Task Empty_rate_and_discount_save_as_zero_like_a_new_line_on_the_purchase_screen()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var service = Service(db);

        await service.ImportAsync(await Preview(service, Header + "01-04-2026,S001,P001,G,7,,,\n"));

        await using var context = db.Open();
        var purchase = await context.Purchases.Include(p => p.PurchaseVariants).SingleAsync(TestContext.Current.CancellationToken);
        var line = Assert.Single(purchase.PurchaseVariants!);
        Assert.Equal(0m, line.ProductRate);   // the product's Rate (250) is NOT used: the screen does not pre-fill it
        Assert.Equal(0m, line.Amount);
        Assert.Equal(0, line.DiscountPercent);
        Assert.Equal(0m, line.AmountAfterDiscount);
        Assert.Equal(0m, purchase.TotalAmountProduct);
        Assert.Null(purchase.Remarks);
    }

    [Fact]
    public async Task Stock_in_hand_rises_by_the_quantities()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var (g, l) = await SampleVariantsAsync(db);
        var service = Service(db);
        var products = new ProductRepositoryEF(db.Factory);

        var before = await products.GetStockInHandByVariantIdsAsync([g, l]);
        Assert.Equal([0, 0], [before[g], before[l]]);

        await service.ImportAsync(await Preview(service, Header + "01-04-2026,S001,P001,G,10,250,,\n01-04-2026,S001,P001,L,5,250,,\n"));

        var after = await products.GetStockInHandByVariantIdsAsync([g, l]);
        Assert.Equal(10, after[g]);
        Assert.Equal(5, after[l]);
    }

    [Fact]
    public async Task Voucher_numbers_continue_after_the_last_one_of_that_date_and_are_consecutive()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var context = db.Open())
        {
            context.Suppliers.Add(new SupplierEntity { SupplierId = "S2", Name = "Second" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // An existing purchase on 01-04-2026 (voucher 1, from S1) and one on 02-04-2026 (voucher 7).
        var purchases = new PurchaseRepositoryEF(db.Factory);
        await purchases.Create(new PurchaseEntity { SupplierEntityId = seed.SupplierId, Date = new DateOnly(2026, 4, 1), VoucherId = 1 });
        await purchases.Create(new PurchaseEntity { SupplierEntityId = seed.SupplierId, Date = new DateOnly(2026, 4, 2), VoucherId = 7 });

        var service = Service(db);
        // Three new purchases: S2 on 01-04 (two rows), S1 on 03-04, S2 on 02-04.
        var preview = await Preview(service,
            Header + "01-04-2026,S2,P1,V1,1,,,\n03-04-2026,S1,P1,V1,1,,,\n02-04-2026,S2,P1,V2,1,,,\n01-04-2026,S2,P1,V2,2,,,\n");
        Assert.True(preview.CanImport, string.Join("; ", Messages(preview.Rows)));
        Assert.Equal(3, preview.RecordCount);
        Assert.Equal(4, preview.LineCount);
        await service.ImportAsync(preview);

        await using var check = db.Open();
        var all = await check.Purchases.Include(p => p.Supplier).ToListAsync(TestContext.Current.CancellationToken);
        int Voucher(string supplier, DateOnly date) => all.Single(p => p.Supplier!.SupplierId == supplier && p.Date == date).VoucherId;
        Assert.Equal(2, Voucher("S2", new DateOnly(2026, 4, 1)));    // after voucher 1
        Assert.Equal(8, Voucher("S2", new DateOnly(2026, 4, 2)));    // after voucher 7
        Assert.Equal(1, Voucher("S1", new DateOnly(2026, 4, 3)));    // first of its date
        Assert.Equal(5, all.Count);
    }

    [Fact]
    public async Task Several_imported_purchases_on_one_date_get_consecutive_numbers_in_file_order()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        await using (var context = db.Open())
        {
            context.Suppliers.AddRange(new SupplierEntity { SupplierId = "S2", Name = "Two" }, new SupplierEntity { SupplierId = "S3", Name = "Three" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var service = Service(db);
        await service.ImportAsync(await Preview(service,
            Header + "05-05-2026,S3,P1,V1,1,,,\n05-05-2026,S1,P1,V1,1,,,\n05-05-2026,S2,P1,V1,1,,,\n"));

        await using var check = db.Open();
        var vouchers = await check.Purchases.Include(p => p.Supplier).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["S3", "S1", "S2"], vouchers.OrderBy(p => p.VoucherId).Select(p => p.Supplier!.SupplierId));
        Assert.Equal([1, 2, 3], vouchers.Select(p => p.VoucherId).Order());
    }

    [Fact]
    public async Task One_bad_row_means_nothing_is_imported()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var service = Service(db);

        var preview = await Preview(service, Header + "01-04-2026,S001,P001,G,10,250,,\n01-04-2026,S001,P001,L,0,250,,\n");
        Assert.False(preview.CanImport);
        Assert.Equal(1, preview.IssueCount);
        Assert.Equal(0, preview.RecordCount);   // counts are given only for a clean file

        var ex = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Contains("has problems", ex.Message);
        await using var context = db.Open();
        Assert.Equal(0, await context.Purchases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.PurchaseVariant.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Preview_reports_every_kind_of_problem_in_a_bad_file()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var service = Service(db);

        var preview = await Preview(service, Header +
            "01-04-2026,NOPE,P001,G,1,,,\n" +
            "01-04-2026,S001,P001,Q,1,,,\n" +
            "31-13-2026,S001,P001,G,1,,,\n" +
            "02-04-2026,S001,P001,G,0,,,\n" +
            "03-04-2026,S001,P001,G,1,,,\n" +
            "03-04-2026,S001,P001,G,2,,,\n");
        var messages = Messages(preview.Rows);
        Assert.Contains("Supplier ID 'NOPE' does not exist. Add the supplier first.", messages);
        Assert.Contains("Variant ID 'Q' does not exist for product 'P001'.", messages);
        Assert.Contains(messages, m => m.StartsWith("Date '31-13-2026' is not a valid date"));
        Assert.Contains("Quantity '0' must be a whole number greater than 0.", messages);
        Assert.Equal(2, messages.Count(m => m.Contains("of the same purchase")));
        Assert.False(preview.CanImport);
    }

    [Fact]
    public async Task If_the_database_changed_after_the_preview_the_import_is_refused_and_nothing_is_added()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = Service(db);
        var preview = await Preview(service, Header + "01-04-2026,S1,P1,V1,4,,,\n");
        Assert.True(preview.CanImport);

        // Someone enters the very same purchase (same supplier, date, item and quantity) in the meantime.
        await using (var setup = db.Open())
        {
            setup.Purchases.Add(new PurchaseEntity
            {
                Id = Guid.NewGuid(), SupplierEntityId = seed.SupplierId, Date = new DateOnly(2026, 4, 1), VoucherId = 1,
                PurchaseVariants = [new PurchaseVariant { Id = Guid.NewGuid(), SerialNumber = 1, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 4 }],
            });
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Contains("already exists (voucher 1)", ex.Message);
        await using var context = db.Open();
        Assert.Equal(1, await context.Purchases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.PurchaseVariant.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task If_the_variant_was_deleted_after_the_preview_the_import_is_refused()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var service = Service(db);
        var preview = await Preview(service, Header + "01-04-2026,S001,P001,G,4,,,\n");
        Assert.True(preview.CanImport);

        await using (var context = db.Open())
        {
            context.Variants.Remove(await context.Variants.SingleAsync(v => v.VariantId == "G", TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Contains("Variant ID 'G' no longer exists for product 'P001'", ex.Message);
        await using var check = db.Open();
        Assert.Equal(0, await check.Purchases.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_safety_backup_is_made_first_and_named_before_import()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        var backups = Backups(db);
        var service = Service(db, backups);

        var preview = await Preview(service, Header + "01-04-2026,S001,P001,G,4,,,\n");
        Assert.Empty(backups.GetBackupFiles());   // previewing never makes a backup
        await service.ImportAsync(preview);

        var file = Assert.Single(backups.GetBackupFiles());
        Assert.StartsWith(BackupService.BeforeImportPrefix, file.Name);
        Assert.Equal("before-import_", BackupService.BeforeImportPrefix);

        // The copy holds the data as it was BEFORE the import: no purchase yet.
        await using var copy = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={file.Path};Mode=ReadOnly").Options);
        Assert.Equal(0, await copy.Purchases.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await copy.Suppliers.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task If_the_safety_backup_fails_nothing_is_imported()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        // A backup "folder" that is really a file cannot be created.
        var blocker = Path.Combine(db.Folder, "not-a-folder");
        await File.WriteAllTextAsync(blocker, "x", TestContext.Current.CancellationToken);
        var service = Service(db, Backups(db, blocker));

        var preview = await Preview(service, Header + "01-04-2026,S001,P001,G,4,,,\n");
        var ex = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.StartsWith("The safety backup could not be made, so nothing was imported.", ex.Message);

        await using var context = db.Open();
        Assert.Equal(0, await context.Purchases.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Other_kinds_do_not_take_a_backup()
    {
        using var db = new TestDatabase();
        var backups = Backups(db);
        var service = Service(db, backups);

        var preview = await service.PreviewAsync(ImportKind.Suppliers, ImportTests.Bytes(ImportTemplates.SampleCsv(ImportKind.Suppliers)), "s.csv");
        await service.ImportAsync(preview);
        Assert.Empty(backups.GetBackupFiles());
    }

    [Fact]
    public async Task An_xlsx_file_with_a_real_date_cell_is_imported()
    {
        using var db = new TestDatabase();
        await SeedSampleAsync(db);
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet();
        string[] headings = ["Date", "Supplier ID", "Product ID", "Variant ID", "Quantity", "Rate", "Discount %", "Remarks"];
        for (var c = 0; c < headings.Length; c++)
            sheet.Cell(1, c + 1).Value = headings[c];
        sheet.Cell(2, 1).Value = new DateTime(2019, 4, 1);   // a real date cell, not text
        sheet.Cell(2, 1).Style.DateFormat.Format = "dd-mmm-yy";
        sheet.Cell(2, 2).Value = "S001";
        sheet.Cell(2, 3).Value = "P001";
        sheet.Cell(2, 4).Value = "G";
        sheet.Cell(2, 5).Value = 12;
        sheet.Cell(2, 6).Value = 80.5;
        sheet.Cell(2, 7).Value = 5;
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var service = Service(db);
        var preview = await service.PreviewAsync(ImportKind.Purchases, stream, "stock.xlsx");
        Assert.True(preview.CanImport, string.Join("; ", Messages(preview.Rows)));
        Assert.Equal("2019-04-01", preview.Rows[0].Cells[0]);   // shown as the reader gave it
        await service.ImportAsync(preview);

        await using var context = db.Open();
        var purchase = await context.Purchases.Include(p => p.PurchaseVariants).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2019, 4, 1), purchase.Date);
        var line = Assert.Single(purchase.PurchaseVariants!);
        Assert.Equal(12, line.Quantity);
        Assert.Equal(80.5m, line.ProductRate);
        Assert.Equal(966m, line.Amount);
        Assert.Equal(917.7m, line.AmountAfterDiscount);
    }

    [Fact]
    public async Task Preview_never_throws_for_a_bad_purchases_file()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var preview = await Preview(service, "Date,Supplier ID\n01-04-2026,S1\n");
        Assert.False(preview.CanImport);
        Assert.Contains(preview.FileIssues, i => i.Message.Contains("'Product ID' is missing"));
    }

    [Fact]
    public async Task Counts_are_zero_for_the_other_kinds()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var preview = await service.PreviewAsync(ImportKind.Customers, ImportTests.Bytes(ImportTemplates.SampleCsv(ImportKind.Customers)), "c.csv");
        Assert.Equal(0, preview.LineCount);
    }
}
