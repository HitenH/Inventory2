namespace Inventory.Service;

/// <summary>
/// Page size for the main lists (batch 14, Hiten's choice): 100 rows by default, and the
/// "Rows per page" drop-down offers 10 / 20 / 50 / 100 / 200. Search and sort still cover all rows.
/// </summary>
public static class ListPaging
{
    public const int DefaultRowsPerPage = 100;
    public static readonly int[] PageSizes = { 10, 20, 50, 100, 200 };
}
