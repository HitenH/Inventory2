using Inventory.Service.Import;

namespace Inventory.Tests;

/// <summary>Batch 8: the row rules for customers, suppliers and products, tested without a database.</summary>
public class ImportRuleTests
{
    private const string PartyHeader = "Customer ID,Name,Mobiles\n";
    private const string ProductHeader = "Product ID,Product Name,Rate,Description,Category ID,Variant ID,Variant Name,Reorder Level\n";

    private static IReadOnlyList<TemplateRow> Rows(string csv)
    {
        var mapped = ImportChecker.MapHeadings(csv.StartsWith("Product") ? ImportKind.Products : ImportKind.Customers,
            ImportFileReader.Read(ImportTests.Bytes(csv), "x.csv"));
        Assert.Empty(mapped.FileIssues);
        return mapped.Rows;
    }

    private static List<ImportPreviewRow> Parties(string body, string[]? existing = null, int maxMobiles = 3) =>
        ImportChecker.CheckParties(ImportKind.Customers, Rows(PartyHeader + body), existing ?? [], maxMobiles);

    private static List<ImportPreviewRow> Products(string body, string[]? existing = null, string[]? categories = null) =>
        ImportChecker.CheckProducts(Rows(ProductHeader + body), existing ?? [], categories ?? []);

    private static string[] Messages(IEnumerable<ImportPreviewRow> rows) => rows.SelectMany(r => r.Issues).Select(i => i.Message).ToArray();

    private static string Only(IEnumerable<ImportPreviewRow> rows) => Assert.Single(Messages(rows));

    // ---- customers and suppliers ----------------------------------------------------------------------------

    [Fact]
    public void Clean_party_rows_have_no_issues()
    {
        Assert.Empty(Messages(Parties("C1,Sharma,9876543210;+91 98765-43210\nC2,Patel,\n")));
    }

    [Fact]
    public void Party_id_and_name_are_required()
    {
        var rows = Parties(",Sharma,\nC2,,\n");
        Assert.Equal("Customer ID is required.", Only(rows.Take(1)));
        Assert.Equal("Name is required.", Only(rows.Skip(1)));
        Assert.Equal(2, rows[0].RowNumber);
    }

    [Fact]
    public void Party_id_over_50_characters_is_refused()
    {
        Assert.Contains("too long", Only(Parties(new string('A', 51) + ",Sharma,\n")));
    }

    [Fact]
    public void Party_id_that_exists_is_refused_ignoring_case()
    {
        var rows = Parties("c001,Sharma,\n", existing: ["C001"]);
        Assert.Equal("Customer ID 'c001' already exists.", Only(rows));
    }

    [Fact]
    public void Same_party_id_twice_flags_every_row_involved()
    {
        var rows = Parties("C001,A,\nC002,B,\nc001,C,\n");
        Assert.Equal("Customer ID 'C001' is also on row 4.", Assert.Single(rows[0].Issues).Message);
        Assert.Empty(rows[1].Issues);
        Assert.Equal("Customer ID 'c001' is also on row 2.", Assert.Single(rows[2].Issues).Message);
    }

    [Fact]
    public void Supplier_rows_use_the_supplier_heading_in_messages()
    {
        var rows = ImportChecker.CheckParties(ImportKind.Suppliers,
            ImportChecker.MapHeadings(ImportKind.Suppliers, ImportFileReader.Read(ImportTests.Bytes("Supplier ID,Name\nS1,A\n"), "x.csv")).Rows, ["s1"], 3);
        Assert.Equal("Supplier ID 'S1' already exists.", Only(rows));
    }

    [Theory]
    [InlineData("98765abc", "is not valid")]
    [InlineData("12345", "must have 6 to 15 digits (it has 5)")]
    [InlineData("1234567890123456", "must have 6 to 15 digits (it has 16)")]
    [InlineData("98765+43210", "is not valid")]
    public void Bad_mobile_numbers_are_refused_naming_the_number(string number, string expected)
    {
        var message = Only(Parties($"C1,A,{number}\n"));
        Assert.Contains($"'{number}'", message);
        Assert.Contains(expected, message);
    }

    [Fact]
    public void Same_mobile_twice_and_too_many_mobiles_are_refused()
    {
        Assert.Contains("more than once", Only(Parties("C1,A,9876543210;98765 43210\n")));
        Assert.Equal("4 mobile numbers are given. At most 3 are allowed.", Only(Parties("C1,A,111111;222222;333333;444444\n")));
        Assert.Empty(Messages(Parties("C1,A,111111;222222;333333;\n")));   // empty pieces are dropped
        Assert.Contains("At most 1", Only(Parties("C1,A,111111;222222\n", maxMobiles: 1)));
    }

    // ---- products -------------------------------------------------------------------------------------------

    [Fact]
    public void Clean_product_rows_have_no_issues()
    {
        var rows = Products("P1,LED,250,12 inch,CAT,G,GANESH,5\nP1,LED,250.00,12 INCH,cat,L,LAXMI,\nP2,Lamp,10,,,,,\n", categories: ["CAT"]);
        Assert.Empty(Messages(rows));
    }

    [Fact]
    public void Product_id_and_name_are_required_on_every_row()
    {
        var rows = Products(",LED,5,,,,,\nP1,,5,,,,,\n");
        Assert.Equal("Product ID is required.", Only(rows.Take(1)));
        Assert.Equal("Product Name is required.", Only(rows.Skip(1).Take(1)));
    }

    [Fact]
    public void Rate_and_description_are_optional_and_an_empty_rate_counts_as_zero()
    {
        // Owner, 14 Nov 2026: Rate and Description need not be filled.
        Assert.Empty(Messages(Products("P2,LED,,,,,,\nP3,Lamp,,,,A,Red,\nP3,Lamp,0,,,B,Blue,\n")));
        Assert.Equal(0m, ImportChecker.BuildProducts(Rows(ProductHeader + "P2,LED,,,,,,\n"))[0].Product.Rate);

        // An empty rate and a filled one on rows of the same product still count as different details.
        var differs = Products("P4,Lamp,,,,A,Red,\nP4,Lamp,250,,,B,Blue,\n");
        Assert.Contains("Rate differs from row 2", Only(differs.Skip(1)));
    }

    [Theory]
    [InlineData("1,250", "not a valid number")]
    [InlineData("Rs 250", "not a valid number")]
    [InlineData("1e3", "not a valid number")]
    [InlineData("1.2.3", "not a valid number")]
    [InlineData("-5", "cannot be negative")]
    public void Bad_rates_are_refused_showing_the_right_form(string rate, string expected)
    {
        // A comma inside the rate must be quoted in the CSV, as Excel would do.
        var message = Only(Products($"P1,LED,\"{rate}\",,,,,\n"));
        Assert.Contains($"'{rate}'", message);
        Assert.Contains(expected, message);
        if (expected.StartsWith("not"))
            Assert.Contains("1250.50", message);
    }

    [Fact]
    public void Good_rates_are_accepted()
    {
        Assert.Empty(Messages(Products("P1,LED,0,,,,,\nP2,LED,250,,,,,\nP3,LED,1250.50,,,,,\n")));
    }

    [Fact]
    public void Product_id_too_long_or_already_existing_is_refused()
    {
        Assert.Contains("too long", Only(Products(new string('P', 51) + ",LED,5,,,,,\n")));
        Assert.Equal("Product ID 'p1' already exists.", Only(Products("p1,LED,5,,,,,\n", existing: ["P1"])));
    }

    [Fact]
    public void Rows_of_one_product_must_repeat_the_same_details()
    {
        var rows = Products("P001,LED Frame,250,12 inch,,G,GANESH,\np001,LED Frame 2,300,13 inch,,L,LAXMI,\n");
        Assert.Empty(rows[0].Issues);
        var messages = rows[1].Issues.Select(i => i.Message).ToList();
        Assert.Contains("Product Name differs from row 2 ('LED Frame'). All rows of product 'p001' must have the same product details.", messages);
        Assert.Contains(messages, m => m.StartsWith("Rate differs from row 2 ('250')"));
        Assert.Contains(messages, m => m.StartsWith("Description differs from row 2 ('12 inch')"));
    }

    [Fact]
    public void Category_difference_and_unknown_category_are_refused()
    {
        var rows = Products("P1,LED,5,,A,G,x,\nP1,LED,5,,B,L,y,\n", categories: ["A", "B"]);
        Assert.Contains("Category ID differs from row 2 ('A')", Only(rows));
        Assert.Equal("Category ID 'Z' does not exist. Add the category first or leave this empty.",
            Only(Products("P1,LED,5,,Z,,,\n", categories: ["A"])));
    }

    [Fact]
    public void Variant_id_and_name_must_both_be_filled_or_both_empty()
    {
        Assert.Equal("Variant Name is required when Variant ID is filled.", Only(Products("P1,LED,5,,,G,,\n")));
        Assert.Equal("Variant ID is required when Variant Name is filled.", Only(Products("P1,LED,5,,,,GANESH,\n")));
    }

    [Fact]
    public void A_row_without_a_variant_is_allowed_only_for_a_single_row_product()
    {
        Assert.Empty(Messages(Products("P1,LED,5,,,,,\n")));
        var rows = Products("P1,LED,5,,,,,\nP1,LED,5,,,G,GANESH,\n");
        Assert.Contains("no Variant ID", Only(rows));
        Assert.Equal(2, rows[0].Issues.Single().RowNumber);
    }

    [Fact]
    public void Same_variant_id_twice_in_a_product_flags_every_row_but_other_products_may_reuse_it()
    {
        var rows = Products("P1,LED,5,,,G,A,\nP1,LED,5,,,g,B,\nP2,Lamp,5,,,G,A,\n");
        Assert.Equal("Variant ID 'G' is also on row 3 of the same product.", Assert.Single(rows[0].Issues).Message);
        Assert.Equal("Variant ID 'g' is also on row 2 of the same product.", Assert.Single(rows[1].Issues).Message);
        Assert.Empty(rows[2].Issues);
    }

    [Fact]
    public void Variant_id_over_50_characters_is_refused()
    {
        Assert.Contains("too long", Only(Products($"P1,LED,5,,,{new string('V', 51)},A,\n")));
    }

    [Fact]
    public void Reorder_level_must_be_a_whole_number_with_a_variant()
    {
        Assert.Empty(Messages(Products("P1,LED,5,,,G,A,0\n")));
        Assert.Contains("whole number", Only(Products("P1,LED,5,,,G,A,2.5\n")));
        Assert.Contains("whole number", Only(Products("P1,LED,5,,,G,A,-1\n")));
        Assert.Contains("Reorder Level can only be given", Only(Products("P1,LED,5,,,,,5\n")));
    }
}
