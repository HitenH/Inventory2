namespace Inventory.Service.Import;

/// <summary>Which master list a file is imported into (batch 8).</summary>
public enum ImportKind { Customers, Suppliers, Products, Purchases }

/// <summary>One column of an import file. <see cref="Header"/> is the exact heading in the sample file.</summary>
public sealed record ImportColumn(string Header, bool Required, string Example1, string Example2, string Hint);

/// <summary>
/// One problem found in a file. <see cref="RowNumber"/> is the row as the operator sees it in Excel
/// (heading row = 1, first data row = 2); null when the problem is about the whole file or its headings.
/// </summary>
public sealed record ImportIssue(int? RowNumber, string? Column, string Message);

/// <summary>One data row of the file as shown in the preview. Cells are in template column order ("" when absent).</summary>
public sealed record ImportPreviewRow(int RowNumber, IReadOnlyList<string> Cells, IReadOnlyList<ImportIssue> Issues);

/// <summary>
/// The result of reading and checking a file. Nothing is saved yet. The screen shows it; the operator then
/// presses Import, which is allowed only when <see cref="CanImport"/> is true (no problems at all).
/// </summary>
public sealed class ImportPreview
{
    public required ImportKind Kind { get; init; }
    public required string FileName { get; init; }
    /// <summary>Problems with the file as a whole: wrong type, missing or unknown headings, no data rows, too many rows.</summary>
    public IReadOnlyList<ImportIssue> FileIssues { get; init; } = [];
    public IReadOnlyList<ImportPreviewRow> Rows { get; init; } = [];
    /// <summary>Customers, suppliers, products or purchases that would be created.</summary>
    public int RecordCount { get; init; }
    /// <summary>Variants that would be created (Products only; otherwise 0).</summary>
    public int VariantCount { get; init; }
    /// <summary>Item lines that would be created (Purchases only; otherwise 0).</summary>
    public int LineCount { get; init; }

    public int IssueCount => FileIssues.Count + Rows.Sum(r => r.Issues.Count);
    public bool CanImport => IssueCount == 0 && RecordCount > 0;

    /// <summary>The checked records, ready to save. Owned by the import service; the screen never looks at it.</summary>
    internal object? Payload { get; init; }
}

/// <summary>An import problem with a message that can be shown to the operator as it is.</summary>
public sealed class ImportException(string message) : Exception(message);

public interface IMasterImportService
{
    /// <summary>
    /// Reads a .csv or .xlsx file and checks every row. Never saves and never throws for a bad file:
    /// every problem comes back as an issue in the preview.
    /// </summary>
    Task<ImportPreview> PreviewAsync(ImportKind kind, Stream file, string fileName);

    /// <summary>
    /// Saves every record of a clean preview in one transaction (all or nothing) and returns how many
    /// customers / suppliers / products were created. Checks the database again first; throws
    /// <see cref="ImportException"/> when the preview has problems or the data changed in the meantime.
    /// </summary>
    Task<int> ImportAsync(ImportPreview preview);
}
