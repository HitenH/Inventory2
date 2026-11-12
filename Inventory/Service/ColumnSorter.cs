namespace Inventory.Service;

/// <summary>
/// Click-to-sort for a table (batch 12; replaces the copy-pasted nine-branch SortItem methods).
/// Behaves like the old code: the first click sorts high-to-low, every click flips the direction
/// (one direction shared by all columns), and an empty list or unknown column changes nothing.
/// </summary>
public sealed class ColumnSorter<T>
{
    private readonly IReadOnlyDictionary<string, Func<T, object?>> columns;
    private bool nextAscending;

    public ColumnSorter(IReadOnlyDictionary<string, Func<T, object?>> columns)
    {
        this.columns = columns;
    }

    public List<T> Sort(List<T> items, string column)
    {
        if (items.Count == 0 || !columns.TryGetValue(column, out var key))
        {
            return items;
        }

        var sorted = nextAscending ? items.OrderBy(key).ToList() : items.OrderByDescending(key).ToList();
        nextAscending = !nextAscending;
        return sorted;
    }
}
