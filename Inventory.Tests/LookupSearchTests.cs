using Inventory.Domain.Entities;
using Inventory.Models;
using Inventory.Shared.Selectors;

namespace Inventory.Tests;

/// <summary>Batch 12: the shared customer / supplier / product pickers search the same way as before.</summary>
public class LookupSearchTests
{
    private static readonly CustomerModel Ann = new()
    {
        CustomerId = "C-100", Name = "Ann Traders", Area = null!, Address = "Main Road",
        Mobiles = new() { new Mobile { Phone = "98765" } },
    };

    [Theory]
    [InlineData("ann", true)]        // name, any case
    [InlineData("c-1", true)]        // customer ID
    [InlineData("main", true)]       // address (search box only)
    [InlineData("  ann  ", true)]    // stray spaces ignored
    [InlineData("", true)]           // empty search shows everyone
    [InlineData("zzz", false)]
    public void Customer_search_box(string search, bool found)
        => Assert.Equal(found, Lookups.Matches(Lookups.Customer.SearchBoxFields(Ann), search));

    [Theory]
    [InlineData("987", true)]        // the customer pop-up also finds phone numbers
    [InlineData("ann", true)]
    [InlineData("main", false)]      // ...but not the address
    public void Customer_popup_search(string search, bool found)
        => Assert.Equal(found, Lookups.Matches(Lookups.Customer.DialogFields(Ann), search));

    [Fact]
    public void Empty_fields_do_not_crash_the_search()
    {
        var blank = new SupplierModel { SupplierId = null!, Name = null!, Mobiles = new() };
        Assert.False(Lookups.Matches(Lookups.Supplier.DialogFields(blank), "x"));
        var noArea = new CustomerModel { CustomerId = null!, Name = null!, Area = null!, Mobiles = new() { new Mobile() } };
        Assert.False(Lookups.Matches(Lookups.Customer.DialogFields(noArea), "x"));
    }

    [Fact]
    public void Product_search_uses_id_and_name()
    {
        var pen = new ProductModel { ProductId = "P7", Name = "Blue Pen", Description = "ink" };
        Assert.True(Lookups.Matches(Lookups.Product.SearchBoxFields(pen), "p7"));
        Assert.True(Lookups.Matches(Lookups.Product.DialogFields(pen), "blue"));
        Assert.False(Lookups.Matches(Lookups.Product.DialogFields(pen), "ink"));
    }

    [Fact]
    public void Search_box_lists_every_match_and_skips_missing_rows()
    {
        // Batch 13: the MudBlazor search box gets its list from SearchBoxMatches – no limit on the number shown.
        var products = Enumerable.Range(1, 30)
            .Select(i => new ProductModel { ProductId = $"P{i}", Name = $"Shirt {i}" })
            .Append(null!)
            .Append(new ProductModel { ProductId = "X1", Name = "Trouser" })
            .ToList();

        Assert.Equal(30, Lookups.SearchBoxMatches(products, Lookups.Product, "shirt").Count());
        Assert.Equal("X1", Assert.Single(Lookups.SearchBoxMatches(products, Lookups.Product, "TROU")).ProductId);
        Assert.Empty(Lookups.SearchBoxMatches(products, Lookups.Product, "zzz"));
    }

    [Fact]
    public void Popup_arrow_keys_move_the_highlight_and_stop_at_the_ends()
    {
        var rows = new List<string> { "A", "B", "C" };
        Assert.Equal("A", Lookups.MoveHighlight(rows, null, 1));     // nothing highlighted yet: first row
        Assert.Equal("B", Lookups.MoveHighlight(rows, "A", 1));
        Assert.Equal("C", Lookups.MoveHighlight(rows, "C", 1));      // stays on the last row
        Assert.Equal("A", Lookups.MoveHighlight(rows, "A", -1));     // stays on the first row
        Assert.Equal("A", Lookups.MoveHighlight(rows, "Z", 1));      // row no longer in the list (new search): first row
        Assert.Null(Lookups.MoveHighlight(new List<string>(), null, 1));
    }

    [Fact]
    public void Search_box_shows_the_saved_choice_of_a_reopened_voucher()
    {
        // Batch 15 bug fix: reopening a voucher showed an empty supplier/customer/product box.
        var supplier = new SupplierModel { SupplierId = "S1", Name = "Acme Traders" };
        Assert.Same(supplier, Lookups.ShownSelection(supplier, Lookups.Supplier));

        // Pages use new() or null for "nothing chosen": the box stays empty.
        Assert.Null(Lookups.ShownSelection(new SupplierModel(), Lookups.Supplier));
        Assert.Null(Lookups.ShownSelection<CustomerModel>(null, Lookups.Customer));
    }
}
