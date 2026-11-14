using System.Text;

namespace Inventory.Service.Import;

/// <summary>
/// The one definition of the import files (batch 8): which columns each file has, which are required, and the
/// sample file the operator downloads. The reader, the checks, the sample and the screen all use this list,
/// so the headings can never drift apart.
/// </summary>
public static class ImportTemplates
{
    public const string MobileSeparator = ";";

    // Heading texts, used by the checks to find a column.
    public const string CustomerId = "Customer ID", SupplierId = "Supplier ID", Name = "Name",
        ContactPerson = "Contact Person", Address = "Address", Area = "Area", Remarks = "Remarks", Mobiles = "Mobiles";
    public const string ProductId = "Product ID", ProductName = "Product Name", Rate = "Rate", Description = "Description",
        CategoryId = "Category ID", VariantId = "Variant ID", VariantName = "Variant Name", ReorderLevel = "Reorder Level";

    private static readonly ImportColumn[] CustomerColumns =
    [
        new(CustomerId, true, "C001", "C002", "Must be new. Upper/lower case does not matter (C001 = c001)."),
        new(Name, true, "Sharma Traders", "Patel Stores", "Customer name."),
        new(ContactPerson, false, "Ramesh Sharma", "", "Optional."),
        new(Address, false, "12, Market Road", "", "Optional."),
        new(Area, false, "Andheri", "Borivali", "Optional."),
        new(Remarks, false, "", "", "Optional."),
        new(Mobiles, false, "9876543210;9123456780", "9988776655", "Optional. Several numbers: separate with a semicolon (;)."),
    ];

    private static readonly ImportColumn[] SupplierColumns =
    [
        new(SupplierId, true, "S001", "S002", "Must be new. Upper/lower case does not matter (S001 = s001)."),
        new(Name, true, "Gupta Wholesale", "Mehta Supplies", "Supplier name."),
        new(ContactPerson, false, "Anil Gupta", "", "Optional."),
        new(Address, false, "45, Industrial Estate", "", "Optional."),
        new(Area, false, "Bhiwandi", "Vasai", "Optional."),
        new(Remarks, false, "", "", "Optional."),
        new(Mobiles, false, "9876543210;9123456780", "9988776655", "Optional. Several numbers: separate with a semicolon (;)."),
    ];

    // One row per variant. A product with three variants has three rows that repeat the product details.
    private static readonly ImportColumn[] ProductColumns =
    [
        new(ProductId, true, "P001", "P001", "Must be new. Repeat it on every row of the same product."),
        new(ProductName, true, "LED Frame", "LED Frame", "Same on every row of the product."),
        new(Rate, false, "250", "250", "Optional (empty = 0). Number, no commas (1250.50). Same on every row of the product."),
        new(Description, false, "12 inch", "12 inch", "Optional. Same on every row of the product."),
        new(CategoryId, false, "", "", "Optional. Must be a Category ID that already exists."),
        new(VariantId, false, "G", "L", "One row per variant. Leave empty for a product without variants."),
        new(VariantName, false, "GANESH", "LAXMI", "Required when Variant ID is filled."),
        new(ReorderLevel, false, "5", "10", "Optional whole number."),
    ];

    public static IReadOnlyList<ImportColumn> Columns(ImportKind kind) => kind switch
    {
        ImportKind.Customers => CustomerColumns,
        ImportKind.Suppliers => SupplierColumns,
        _ => ProductColumns,
    };

    /// <summary>"customer" / "customers" etc. for messages.</summary>
    public static string Noun(ImportKind kind, int count = 1) => kind switch
    {
        ImportKind.Customers => count == 1 ? "customer" : "customers",
        ImportKind.Suppliers => count == 1 ? "supplier" : "suppliers",
        _ => count == 1 ? "product" : "products",
    };

    public static string SampleFileName(ImportKind kind) => $"{Noun(kind, 2)}-import-sample.csv";

    /// <summary>The sample file: the heading row and two example rows.</summary>
    public static string SampleCsv(ImportKind kind)
    {
        var columns = Columns(kind);
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));
        text.AppendLine(string.Join(",", columns.Select(c => Escape(c.Example1))));
        text.AppendLine(string.Join(",", columns.Select(c => Escape(c.Example2))));
        return text.ToString();
    }

    /// <summary>CSV rule: a value with a comma, quote or line break goes in quotes, and quotes are doubled.</summary>
    public static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
