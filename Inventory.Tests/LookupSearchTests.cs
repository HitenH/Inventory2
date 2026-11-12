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
}
