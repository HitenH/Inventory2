using System.Globalization;
using System.Text.RegularExpressions;
using Inventory.Domain.Entities;

namespace Inventory.Service.Import;

/// <summary>One data row with its cells arranged in template column order ("" for a column the file does not have).</summary>
public sealed record TemplateRow(int RowNumber, IReadOnlyList<string> Cells);

/// <summary>The outcome of matching the file's headings to the template.</summary>
public sealed record MappedTable(IReadOnlyList<ImportIssue> FileIssues, IReadOnlyList<TemplateRow> Rows);

/// <summary>
/// The import rules (batch 8) as pure functions: they take the file's text and the IDs that already exist,
/// and return problems. No database here, so every rule can be tested on its own.
/// </summary>
public static class ImportChecker
{
    public const int MaxIdLength = 50;
    public const int MinPhoneDigits = 6, MaxPhoneDigits = 15;
    public const int DefaultMaxMobiles = 3;

    private static readonly Regex PhoneShape = new(@"^\+?[0-9 \-]+$", RegexOptions.Compiled);
    private static readonly Regex RateShape = new(@"^(\d+(\.\d*)?|\.\d+)$", RegexOptions.Compiled);
    private static readonly Regex WholeNumberShape = new(@"^\d+$", RegexOptions.Compiled);

    // Column positions in the template (the order of ImportTemplates.Columns). Customers and suppliers share a layout.
    private const int PartyId = 0, PartyName = 1, PartyMobiles = 6;
    private const int ProdId = 0, ProdName = 1, ProdRate = 2, ProdDescription = 3, ProdCategory = 4,
        ProdVariantId = 5, ProdVariantName = 6, ProdReorder = 7;

    // ---- headings -------------------------------------------------------------------------------------------

    /// <summary>"Customer_Id", " customer id *" and "CustomerID" all become "customerid".</summary>
    public static string NormalizeHeading(string heading) =>
        new string(heading.Trim().TrimEnd('*').Where(c => c != ' ' && c != '_' && c != '-' && !char.IsWhiteSpace(c)).ToArray())
            .ToLowerInvariant();

    public static MappedTable MapHeadings(ImportKind kind, RawTable table)
    {
        var columns = ImportTemplates.Columns(kind);
        var issues = new List<ImportIssue>();
        var map = Enumerable.Repeat(-1, columns.Count).ToArray();   // template column -> file column

        if (table.Headings.All(h => h.Trim().Length == 0) && table.Rows.Count == 0)
        {
            issues.Add(new ImportIssue(null, null, "The file is empty."));
            return new MappedTable(issues, []);
        }

        for (var f = 0; f < table.Headings.Count; f++)
        {
            var heading = table.Headings[f].Trim();
            if (heading.Length == 0)
            {
                if (table.Rows.Any(r => r.Cells[f].Trim().Length > 0))
                    issues.Add(new ImportIssue(null, null, $"Column {ColumnName(f)} has data but no heading. Add a heading or remove the data."));
                continue;
            }

            var key = NormalizeHeading(heading);
            var t = -1;
            for (var i = 0; i < columns.Count; i++)
                if (NormalizeHeading(columns[i].Header) == key)
                    t = i;

            if (t < 0)
                issues.Add(new ImportIssue(null, heading, $"Column '{heading}' is not recognised. Allowed columns: {string.Join(", ", columns.Select(c => c.Header))}."));
            else if (map[t] >= 0)
                issues.Add(new ImportIssue(null, columns[t].Header, $"Column '{columns[t].Header}' appears more than once."));
            else
                map[t] = f;
        }

        for (var i = 0; i < columns.Count; i++)
            if (columns[i].Required && map[i] < 0)
                issues.Add(new ImportIssue(null, columns[i].Header, $"Column '{columns[i].Header}' is missing."));

        if (table.Rows.Count == 0)
            issues.Add(new ImportIssue(null, null, "The file has headings but no rows."));

        var rows = table.Rows
            .Select(r => new TemplateRow(r.RowNumber, map.Select(f => f < 0 ? "" : r.Cells[f].Trim()).ToList()))
            .ToList();
        return new MappedTable(issues, rows);
    }

    /// <summary>Spreadsheet-style column letter: A, B ... Z, AA.</summary>
    private static string ColumnName(int index)
    {
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    // ---- customers and suppliers ----------------------------------------------------------------------------

    public static List<ImportPreviewRow> CheckParties(ImportKind kind, IReadOnlyList<TemplateRow> rows,
        IEnumerable<string?> existingIds, int maxMobiles)
    {
        var columns = ImportTemplates.Columns(kind);
        var idHeader = columns[PartyId].Header;
        var nameHeader = columns[PartyName].Header;
        var mobilesHeader = columns[PartyMobiles].Header;
        var existing = new HashSet<string>(existingIds.Where(i => i != null)!, StringComparer.OrdinalIgnoreCase);
        var issues = NewIssueLists(rows);

        for (var r = 0; r < rows.Count; r++)
        {
            var id = Cell(rows[r], PartyId);
            if (id.Length == 0)
                issues[r].Add(Issue(rows[r], idHeader, $"{idHeader} is required."));
            else
            {
                if (id.Length > MaxIdLength)
                    issues[r].Add(Issue(rows[r], idHeader, $"{idHeader} '{id}' is too long ({id.Length} characters). The most allowed is {MaxIdLength}."));
                if (existing.Contains(id))
                    issues[r].Add(Issue(rows[r], idHeader, $"{idHeader} '{id}' already exists."));
            }

            if (Cell(rows[r], PartyName).Length == 0)
                issues[r].Add(Issue(rows[r], nameHeader, $"{nameHeader} is required."));

            CheckMobiles(Cell(rows[r], PartyMobiles), mobilesHeader, maxMobiles, rows[r], issues[r]);
        }

        AddRepeats(rows, issues, r => Cell(r, PartyId), idHeader, (id, where) => $"{idHeader} '{id}' is also on {where}.");
        return Build(rows, issues);
    }

    private static void CheckMobiles(string text, string header, int maxMobiles, TemplateRow row, List<ImportIssue> issues)
    {
        var numbers = text.Split(ImportTemplates.MobileSeparator[0])
            .Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
        if (numbers.Count > maxMobiles)
            issues.Add(Issue(row, header, $"{numbers.Count} mobile numbers are given. At most {maxMobiles} are allowed."));

        var seen = new HashSet<string>();
        foreach (var number in numbers)
        {
            var digits = number.Count(char.IsAsciiDigit);
            if (!PhoneShape.IsMatch(number))
                issues.Add(Issue(row, header, $"Mobile number '{number}' is not valid. Use only digits, spaces and '-', with an optional '+' at the start."));
            else if (digits < MinPhoneDigits || digits > MaxPhoneDigits)
                issues.Add(Issue(row, header, $"Mobile number '{number}' must have {MinPhoneDigits} to {MaxPhoneDigits} digits (it has {digits})."));
            else if (!seen.Add(new string(number.Where(c => c != ' ' && c != '-').ToArray())))
                issues.Add(Issue(row, header, $"Mobile number '{number}' is given more than once."));
        }
    }

    // ---- products -------------------------------------------------------------------------------------------

    public static List<ImportPreviewRow> CheckProducts(IReadOnlyList<TemplateRow> rows,
        IEnumerable<string?> existingProductIds, IEnumerable<string?> existingCategoryIds)
    {
        var existing = new HashSet<string>(existingProductIds.Where(i => i != null)!, StringComparer.OrdinalIgnoreCase);
        var categories = new HashSet<string>(existingCategoryIds.Where(i => i != null)!, StringComparer.OrdinalIgnoreCase);
        var issues = NewIssueLists(rows);

        // Rows with the same Product ID (ignoring case) are one product; the first row is the reference for the others.
        var firstRowOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rowCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var r = 0; r < rows.Count; r++)
        {
            var id = Cell(rows[r], ProdId);
            if (id.Length == 0) continue;
            firstRowOf.TryAdd(id, r);
            rowCount[id] = rowCount.GetValueOrDefault(id) + 1;
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var list = issues[r];
            var id = Cell(row, ProdId);

            if (id.Length == 0)
                list.Add(Issue(row, ImportTemplates.ProductId, "Product ID is required."));
            else
            {
                if (id.Length > MaxIdLength)
                    list.Add(Issue(row, ImportTemplates.ProductId, $"Product ID '{id}' is too long ({id.Length} characters). The most allowed is {MaxIdLength}."));
                if (existing.Contains(id))
                    list.Add(Issue(row, ImportTemplates.ProductId, $"Product ID '{id}' already exists."));
            }

            if (Cell(row, ProdName).Length == 0)
                list.Add(Issue(row, ImportTemplates.ProductName, "Product Name is required."));

            var rate = ParseRate(Cell(row, ProdRate), row, list);

            var category = Cell(row, ProdCategory);
            if (category.Length > 0 && !categories.Contains(category))
                list.Add(Issue(row, ImportTemplates.CategoryId, $"Category ID '{category}' does not exist. Add the category first or leave this empty."));

            // Every row of one product must repeat the same product details.
            if (id.Length > 0 && firstRowOf[id] != r)
            {
                var first = rows[firstRowOf[id]];
                CheckSame(row, first, ProdName, ImportTemplates.ProductName, id, list);
                CheckSame(row, first, ProdDescription, ImportTemplates.Description, id, list);
                CheckSame(row, first, ProdCategory, ImportTemplates.CategoryId, id, list);
                if (rate != null && RateOrZero(Cell(first, ProdRate)) is { } firstRate && firstRate != rate)
                    list.Add(Issue(row, ImportTemplates.Rate, DiffersMessage(ImportTemplates.Rate, first, Cell(first, ProdRate), id)));
            }

            CheckVariant(row, id, id.Length > 0 && rowCount[id] > 1, list);
        }

        // The same Variant ID twice inside one product.
        var variantGroups = Enumerable.Range(0, rows.Count)
            .Where(r => Cell(rows[r], ProdId).Length > 0 && Cell(rows[r], ProdVariantId).Length > 0)
            .GroupBy(r => (Cell(rows[r], ProdId).ToLowerInvariant(), Cell(rows[r], ProdVariantId).ToLowerInvariant()));
        foreach (var group in variantGroups.Where(g => g.Count() > 1))
            foreach (var r in group)
            {
                var others = group.Where(o => o != r).Select(o => rows[o].RowNumber).ToList();
                issues[r].Add(Issue(rows[r], ImportTemplates.VariantId,
                    $"Variant ID '{Cell(rows[r], ProdVariantId)}' is also on {RowsText(others)} of the same product."));
            }

        return Build(rows, issues);
    }

    private static void CheckVariant(TemplateRow row, string productId, bool productHasSeveralRows, List<ImportIssue> list)
    {
        var variantId = Cell(row, ProdVariantId);
        var variantName = Cell(row, ProdVariantName);
        var reorder = Cell(row, ProdReorder);

        if (variantId.Length > 0 && variantName.Length == 0)
            list.Add(Issue(row, ImportTemplates.VariantName, "Variant Name is required when Variant ID is filled."));
        if (variantName.Length > 0 && variantId.Length == 0)
            list.Add(Issue(row, ImportTemplates.VariantId, "Variant ID is required when Variant Name is filled."));
        if (variantId.Length > MaxIdLength)
            list.Add(Issue(row, ImportTemplates.VariantId, $"Variant ID '{variantId}' is too long ({variantId.Length} characters). The most allowed is {MaxIdLength}."));

        if (variantId.Length == 0 && variantName.Length == 0 && productHasSeveralRows)
            list.Add(Issue(row, ImportTemplates.VariantId,
                $"This row has no Variant ID. A row without a variant is allowed only when product '{productId}' has just this one row."));

        if (reorder.Length > 0)
        {
            if (!WholeNumberShape.IsMatch(reorder) || !int.TryParse(reorder, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                list.Add(Issue(row, ImportTemplates.ReorderLevel, $"Reorder Level '{reorder}' must be a whole number, 0 or more."));
            else if (variantId.Length == 0)
                list.Add(Issue(row, ImportTemplates.ReorderLevel, "Reorder Level can only be given on a row that has a Variant ID."));
        }
    }

    /// <summary>Checks a Rate cell; returns the number when it is valid, otherwise adds an issue and returns null.</summary>
    private static decimal? ParseRate(string text, TemplateRow row, List<ImportIssue> list)
    {
        // Rate is optional (owner, 14 Nov 2026): an empty cell is saved as 0, like the Rate box on a new product.
        if (text.Length == 0)
            return 0m;

        if (text.StartsWith('-') && TryRate(text.TrimStart('-')) != null)
        {
            list.Add(Issue(row, ImportTemplates.Rate, $"Rate '{text}' cannot be negative."));
            return null;
        }

        var rate = TryRate(text);
        if (rate == null)
            list.Add(Issue(row, ImportTemplates.Rate, $"Rate '{text}' is not a valid number. Type digits only, with a decimal point if needed, like 250 or 1250.50 (no commas or currency signs)."));
        return rate;
    }

    /// <summary>An empty Rate cell counts as 0; otherwise the number, or null when it is not a valid rate.</summary>
    private static decimal? RateOrZero(string text) => text.Length == 0 ? 0m : TryRate(text);

    private static decimal? TryRate(string text) =>
        RateShape.IsMatch(text) && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static void CheckSame(TemplateRow row, TemplateRow first, int index, string header, string productId, List<ImportIssue> list)
    {
        var mine = Cell(row, index);
        var theirs = Cell(first, index);
        if (!string.Equals(mine, theirs, StringComparison.OrdinalIgnoreCase))
            list.Add(Issue(row, header, DiffersMessage(header, first, theirs, productId)));
    }

    private static string DiffersMessage(string header, TemplateRow first, string firstValue, string productId) =>
        $"{header} differs from row {first.RowNumber} ({(firstValue.Length == 0 ? "empty" : $"'{firstValue}'")}). " +
        $"All rows of product '{productId}' must have the same product details.";

    // ---- shared helpers -------------------------------------------------------------------------------------

    private static string Cell(TemplateRow row, int index) => (row.Cells[index] ?? "").Trim();

    private static ImportIssue Issue(TemplateRow row, string column, string message) => new(row.RowNumber, column, message);

    private static List<ImportIssue>[] NewIssueLists(IReadOnlyList<TemplateRow> rows) =>
        rows.Select(_ => new List<ImportIssue>()).ToArray();

    private static List<ImportPreviewRow> Build(IReadOnlyList<TemplateRow> rows, List<ImportIssue>[] issues) =>
        rows.Select((r, i) => new ImportPreviewRow(r.RowNumber, r.Cells, issues[i])).ToList();

    /// <summary>"row 7", "rows 3 and 7", "rows 3, 5 and 7".</summary>
    private static string RowsText(IReadOnlyList<int> numbers) => numbers.Count switch
    {
        1 => $"row {numbers[0]}",
        2 => $"rows {numbers[0]} and {numbers[1]}",
        _ => $"rows {string.Join(", ", numbers.Take(numbers.Count - 1))} and {numbers[^1]}",
    };

    /// <summary>Flags every row whose value appears on another row too (ignoring case), naming the other rows.</summary>
    private static void AddRepeats(IReadOnlyList<TemplateRow> rows, List<ImportIssue>[] issues,
        Func<TemplateRow, string> valueOf, string column, Func<string, string, string> message)
    {
        var groups = Enumerable.Range(0, rows.Count)
            .Where(r => valueOf(rows[r]).Length > 0)
            .GroupBy(r => valueOf(rows[r]), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var group in groups)
            foreach (var r in group)
            {
                var others = group.Where(o => o != r).Select(o => rows[o].RowNumber).ToList();
                issues[r].Add(Issue(rows[r], column, message(valueOf(rows[r]), RowsText(others))));
            }
    }

    // ---- building the records to save (only called for a file without problems) -----------------------------

    public static List<CustomerEntity> BuildCustomers(IReadOnlyList<TemplateRow> rows) =>
        rows.Select(r => new CustomerEntity
        {
            Id = Guid.NewGuid(),
            CustomerId = Cell(r, 0),
            Name = Cell(r, 1),
            ContactPerson = Cell(r, 2),
            Address = Cell(r, 3),
            Area = Cell(r, 4),
            Remarks = Cell(r, 5),
            Mobiles = MobilesOf(Cell(r, PartyMobiles)),
        }).ToList();

    public static List<SupplierEntity> BuildSuppliers(IReadOnlyList<TemplateRow> rows) =>
        rows.Select(r => new SupplierEntity
        {
            Id = Guid.NewGuid(),
            SupplierId = Cell(r, 0),
            Name = Cell(r, 1),
            ContactPerson = Cell(r, 2),
            Address = Cell(r, 3),
            Area = Cell(r, 4),
            Remarks = Cell(r, 5),
            Mobiles = MobilesOf(Cell(r, PartyMobiles)),
        }).ToList();

    private static List<Mobile> MobilesOf(string text) =>
        text.Split(ImportTemplates.MobileSeparator[0])
            .Select(n => n.Trim()).Where(n => n.Length > 0)
            .Select(n => new Mobile { Phone = n }).ToList();

    public static List<ProductDraft> BuildProducts(IReadOnlyList<TemplateRow> rows)
    {
        var drafts = new List<ProductDraft>();
        foreach (var group in rows.GroupBy(r => Cell(r, ProdId), StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var product = new ProductEntity
            {
                Id = Guid.NewGuid(),
                ProductId = Cell(first, ProdId),
                Name = Cell(first, ProdName),
                Rate = RateOrZero(Cell(first, ProdRate)),
                Description = Cell(first, ProdDescription),
                Variants = [],
            };
            foreach (var row in group.Where(r => Cell(r, ProdVariantId).Length > 0))
            {
                product.Variants.Add(new VariantEntity
                {
                    Id = Guid.NewGuid(),
                    VariantId = Cell(row, ProdVariantId),
                    Name = Cell(row, ProdVariantName),
                    // An empty box saves "no reorder level" (null), as on the Variant screen.
                    ReorderLevel = Cell(row, ProdReorder) is { Length: > 0 } level ? int.Parse(level, CultureInfo.InvariantCulture) : null,
                    Product = product,
                });
            }

            var category = Cell(first, ProdCategory);
            drafts.Add(new ProductDraft(product, category.Length == 0 ? null : category));
        }

        return drafts;
    }
}

/// <summary>A product ready to save, plus the Category ID it should be linked to (null = no category).</summary>
public sealed record ProductDraft(ProductEntity Product, string? CategoryId);
