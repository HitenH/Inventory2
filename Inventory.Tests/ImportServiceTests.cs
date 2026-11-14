using Inventory.Domain.Entities;
using Inventory.Service.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.Tests;

/// <summary>Batch 8: the import service against a real (temporary) database.</summary>
public class ImportServiceTests
{
    private static MasterImportService Service(TestDatabase db, int? maxMobiles = null)
    {
        var settings = new Dictionary<string, string?>();
        if (maxMobiles != null)
        {
            settings["CustomerNumberMobile"] = maxMobiles.ToString();
            settings["SupplierNumberMobile"] = maxMobiles.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new MasterImportService(db.Factory, NullLogger<MasterImportService>.Instance, configuration);
    }

    private static Task<ImportPreview> Preview(MasterImportService service, ImportKind kind, string csv, string name = "file.csv") =>
        service.PreviewAsync(kind, ImportTests.Bytes(csv), name);

    private const string ProductHeader = "Product ID,Product Name,Rate,Description,Category ID,Variant ID,Variant Name,Reorder Level\n";

    // ---- sample files ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ImportKind.Customers)]
    [InlineData(ImportKind.Suppliers)]
    [InlineData(ImportKind.Products)]
    public async Task Sample_file_previews_clean_imports_and_a_second_import_creates_nothing(ImportKind kind)
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var sample = ImportTemplates.SampleCsv(kind);

        var preview = await Preview(service, kind, sample, ImportTemplates.SampleFileName(kind));
        Assert.Empty(preview.FileIssues);
        Assert.Equal(0, preview.IssueCount);
        Assert.True(preview.CanImport);
        Assert.Equal(kind == ImportKind.Products ? 1 : 2, preview.RecordCount);
        Assert.Equal(kind == ImportKind.Products ? 2 : 0, preview.VariantCount);

        Assert.Equal(preview.RecordCount, await service.ImportAsync(preview));
        var count = await CountAsync(db, kind);
        Assert.Equal(preview.RecordCount, count);

        // The same preview object cannot be saved twice.
        var again = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Contains("already exist", again.Message);
        Assert.Equal(count, await CountAsync(db, kind));

        // Previewing the same file again flags every record.
        var second = await Preview(service, kind, sample);
        Assert.False(second.CanImport);
        var idRows = second.Rows.Where(r => r.Issues.Any(i => i.Message.Contains("already exists"))).Count();
        Assert.Equal(second.Rows.Count, idRows);
    }

    private static async Task<int> CountAsync(TestDatabase db, ImportKind kind)
    {
        await using var context = db.Open();
        return kind switch
        {
            ImportKind.Customers => await context.Customers.CountAsync(),
            ImportKind.Suppliers => await context.Suppliers.CountAsync(),
            _ => await context.Products.CountAsync(),
        };
    }

    // ---- preview --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Preview_never_throws_for_a_bad_file()
    {
        using var db = new TestDatabase();
        var service = Service(db);

        var wrongType = await Preview(service, ImportKind.Customers, "a", "list.txt");
        Assert.Contains("Only .csv and .xlsx", Assert.Single(wrongType.FileIssues).Message);
        Assert.False(wrongType.CanImport);

        var broken = await Preview(service, ImportKind.Customers, "garbage", "list.xlsx");
        Assert.Contains("could not be opened", Assert.Single(broken.FileIssues).Message);

        var headings = await Preview(service, ImportKind.Customers, "Name,Phone\nA,1\n");
        Assert.Equal(2, headings.FileIssues.Count);   // Customer ID missing + Phone unknown
        Assert.Equal(0, headings.RecordCount);
        Assert.Single(headings.Rows);
        Assert.Empty(headings.Rows[0].Issues);
    }

    [Fact]
    public async Task Preview_of_an_xlsx_file_works()
    {
        using var db = new TestDatabase();
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet();
        sheet.Cell(1, 1).Value = "Customer ID";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Mobiles";
        sheet.Cell(2, 1).Value = "C1";
        sheet.Cell(2, 2).Value = "Sharma";
        sheet.Cell(2, 3).Value = 919876543210d;
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var service = Service(db);
        var preview = await service.PreviewAsync(ImportKind.Customers, stream, "list.xlsx");
        Assert.True(preview.CanImport);
        await service.ImportAsync(preview);

        await using var context = db.Open();
        var customer = await context.Customers.Include(c => c.Mobiles).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("919876543210", Assert.Single(customer.Mobiles!).Phone);
    }

    [Fact]
    public async Task Mobile_limit_comes_from_configuration()
    {
        using var db = new TestDatabase();
        var csv = "Customer ID,Name,Mobiles\nC1,A,111111;222222;333333\n";
        Assert.True((await Preview(Service(db), ImportKind.Customers, csv)).CanImport);   // default 3
        Assert.False((await Preview(Service(db, maxMobiles: 2), ImportKind.Customers, csv)).CanImport);
    }

    // ---- saving ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Customers_are_saved_with_their_mobiles_as_typed()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var preview = await Preview(service, ImportKind.Customers,
            "Customer ID,Name,Contact Person,Area,Mobiles\nC1,Sharma,Ramesh,Andheri,\"+91 98765-43210; 9123456780\"\nC2,Patel,,,\n");
        Assert.Equal(2, await service.ImportAsync(preview));

        await using var context = db.Open();
        var c1 = await context.Customers.Include(c => c.Mobiles).SingleAsync(c => c.CustomerId == "C1", TestContext.Current.CancellationToken);
        Assert.Equal("Sharma", c1.Name);
        Assert.Equal("Ramesh", c1.ContactPerson);
        Assert.Equal("Andheri", c1.Area);
        Assert.Equal(["+91 98765-43210", "9123456780"], c1.Mobiles!.Select(m => m.Phone).OrderBy(p => p));
        Assert.NotEqual(Guid.Empty, c1.Id);
    }

    [Fact]
    public async Task Suppliers_are_saved_with_their_mobiles()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var preview = await Preview(service, ImportKind.Suppliers, "Supplier ID,Name,Mobiles\nS1,Gupta,9876543210\n");
        await service.ImportAsync(preview);

        await using var context = db.Open();
        var supplier = await context.Suppliers.Include(s => s.Mobiles).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("9876543210", Assert.Single(supplier.Mobiles!).Phone);
    }

    [Fact]
    public async Task A_file_with_one_bad_row_imports_nothing()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var preview = await Preview(service, ImportKind.Customers, "Customer ID,Name\nC1,A\nC2,\nC3,C\n");
        Assert.False(preview.CanImport);
        Assert.Equal(1, preview.IssueCount);
        await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Equal(0, await CountAsync(db, ImportKind.Customers));
    }

    [Fact]
    public async Task A_clash_with_an_existing_record_is_found_ignoring_case_and_nothing_is_saved()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();   // customer C1, supplier S1, product P1
        var service = Service(db);

        var preview = await Preview(service, ImportKind.Customers, "Customer ID,Name\nc1,Clash\nC9,New\n");
        Assert.Equal("Customer ID 'c1' already exists.", Assert.Single(preview.Rows[0].Issues).Message);
        Assert.False(preview.CanImport);
        Assert.Equal(1, await CountAsync(db, ImportKind.Customers));

        var products = await Preview(service, ImportKind.Products, ProductHeader + "p1,Clash,5,,,,,\n");
        Assert.Equal("Product ID 'p1' already exists.", Assert.Single(products.Rows[0].Issues).Message);
    }

    [Fact]
    public async Task A_record_added_after_the_preview_stops_the_import_and_saves_nothing()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var preview = await Preview(service, ImportKind.Customers, "Customer ID,Name\nC1,A\nC2,B\n");
        Assert.True(preview.CanImport);

        await using (var context = db.Open())
        {
            context.Customers.Add(new CustomerEntity { CustomerId = "c2", Name = "Someone else" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Equal("Customer ID 'C2' already exists. Nothing was imported.", ex.Message);
        Assert.Equal(1, await CountAsync(db, ImportKind.Customers));
    }

    [Fact]
    public async Task Import_of_a_preview_with_problems_is_refused()
    {
        using var db = new TestDatabase();
        var preview = await Preview(Service(db), ImportKind.Customers, "Customer ID,Name\n,\n");
        await Assert.ThrowsAsync<ImportException>(() => Service(db).ImportAsync(preview));
    }

    [Fact]
    public async Task Products_are_saved_with_variants_reorder_levels_and_category()
    {
        using var db = new TestDatabase();
        Guid categoryId;
        await using (var context = db.Open())
        {
            var category = new CategoryEntity { CategoryId = "FRAMES", Name = "Frames" };
            context.Categories.Add(category);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            categoryId = category.Id;
        }

        var service = Service(db);
        var preview = await Preview(service, ImportKind.Products, ProductHeader +
            "P1,LED Frame,250.50,12 inch,frames,G,GANESH,5\n" +
            "P1,LED Frame,250.5,12 inch,FRAMES,L,LAXMI,\n" +
            "P2,Plain,10,,,,,\n");
        Assert.True(preview.CanImport, string.Join(" | ", preview.Rows.SelectMany(r => r.Issues).Select(i => i.Message)));
        Assert.Equal(2, preview.RecordCount);
        Assert.Equal(2, preview.VariantCount);
        Assert.Equal(2, await service.ImportAsync(preview));

        await using var check = db.Open();
        var p1 = await check.Products.Include(p => p.Category).Include(p => p.Variants).SingleAsync(p => p.ProductId == "P1", TestContext.Current.CancellationToken);
        Assert.Equal(250.5m, p1.Rate);
        Assert.Equal("12 inch", p1.Description);
        Assert.Equal(categoryId, p1.Category?.Id);
        Assert.Equal(2, p1.Variants!.Count);
        Assert.Equal(5, p1.Variants.Single(v => v.VariantId == "G").ReorderLevel);
        Assert.Null(p1.Variants.Single(v => v.VariantId == "L").ReorderLevel);
        Assert.Equal("LAXMI", p1.Variants.Single(v => v.VariantId == "L").Name);

        var p2 = await check.Products.Include(p => p.Category).Include(p => p.Variants).SingleAsync(p => p.ProductId == "P2", TestContext.Current.CancellationToken);
        Assert.Null(p2.Category);
        Assert.Empty(p2.Variants!);
        Assert.Equal(1, await check.Categories.CountAsync(TestContext.Current.CancellationToken));   // no duplicate category created
    }

    [Fact]
    public async Task Unknown_category_is_reported_and_a_category_removed_after_the_preview_stops_the_import()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var unknown = await Preview(service, ImportKind.Products, ProductHeader + "P1,A,5,,NOPE,,,\n");
        Assert.Contains("Category ID 'NOPE' does not exist", Assert.Single(unknown.Rows[0].Issues).Message);

        await using (var context = db.Open())
        {
            context.Categories.Add(new CategoryEntity { CategoryId = "C", Name = "C" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var preview = await Preview(service, ImportKind.Products, ProductHeader + "P1,A,5,,C,,,\n");
        Assert.True(preview.CanImport);
        await using (var context = db.Open())
        {
            context.Categories.RemoveRange(context.Categories);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var ex = await Assert.ThrowsAsync<ImportException>(() => service.ImportAsync(preview));
        Assert.Contains("no longer exists", ex.Message);
        Assert.Equal(0, await CountAsync(db, ImportKind.Products));
    }
}
