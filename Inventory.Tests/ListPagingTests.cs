using Inventory.MudBlazorComponents;
using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Batch 14: lists show 100 rows by default; deletes of several rows say how many.</summary>
public class ListPagingTests
{
    [Fact]
    public void Default_page_size_is_one_of_the_choices_offered()
    {
        Assert.Equal(new[] { 10, 20, 50, 100, 200 }, ListPaging.PageSizes);
        Assert.Contains(ListPaging.DefaultRowsPerPage, ListPaging.PageSizes);
        Assert.Equal(100, ListPaging.DefaultRowsPerPage);
    }

    [Theory]
    [InlineData(1, "Do you really want to delete this 1 record? This process cannot be undone.")]
    [InlineData(0, "Do you really want to delete these 0 records? This process cannot be undone.")]
    [InlineData(250, "Do you really want to delete these 250 records? This process cannot be undone.")]
    public void Delete_warning_states_the_number_of_records(int count, string expected)
        => Assert.Equal(expected, DialogServiceExtensions.DeleteWarningFor(count));
}
