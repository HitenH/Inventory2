using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Click-to-sort on the purchase and sales-order line tables (batch 12).</summary>
public class ColumnSorterTests
{
    private record Line(int Serial, string Name, int? Quantity);

    private static ColumnSorter<Line> NewSorter() => new(new Dictionary<string, Func<Line, object?>>
    {
        { "SerialNumber", l => l.Serial },
        { "ProductName", l => l.Name },
        { "Quantity", l => l.Quantity },
    });

    private static readonly List<Line> Lines = new()
    {
        new(1, "Bag", 5), new(2, "Apron", null), new(3, "Cap", 2),
    };

    [Fact]
    public void First_click_sorts_high_to_low_then_each_click_flips()
    {
        var sorter = NewSorter();
        Assert.Equal(new[] { 3, 2, 1 }, sorter.Sort(Lines, "SerialNumber").Select(l => l.Serial));
        Assert.Equal(new[] { 1, 2, 3 }, sorter.Sort(Lines, "SerialNumber").Select(l => l.Serial));
        // The direction is shared by all columns, as before: the third click (another column) is high-to-low again.
        Assert.Equal(new[] { "Cap", "Bag", "Apron" }, sorter.Sort(Lines, "ProductName").Select(l => l.Name));
    }

    [Fact]
    public void Empty_quantity_counts_as_smallest()
    {
        var sorter = NewSorter();
        Assert.Equal(new int?[] { 5, 2, null }, sorter.Sort(Lines, "Quantity").Select(l => l.Quantity));
    }

    [Fact]
    public void Empty_list_or_unknown_column_changes_nothing_and_does_not_flip()
    {
        var sorter = NewSorter();
        Assert.Empty(sorter.Sort(new List<Line>(), "SerialNumber"));
        Assert.Same(Lines, sorter.Sort(Lines, "NoSuchColumn"));
        Assert.Equal(new[] { 3, 2, 1 }, sorter.Sort(Lines, "SerialNumber").Select(l => l.Serial));
    }
}
