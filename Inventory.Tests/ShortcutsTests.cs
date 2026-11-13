using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Keyboard shortcuts (owner's request, Nov 2026): Alt + letter opens a screen, Alt+Shift opens a new one, Alt+N = new here.</summary>
public class ShortcutsTests
{
    [Theory]
    [InlineData('H', "/")]
    [InlineData('C', "/customers")]
    [InlineData('U', "/suppliers")]
    [InlineData('I', "/products")]
    [InlineData('P', "/purchases")]
    [InlineData('Q', "/purchaseorder")]
    [InlineData('O', "/salesorders")]
    [InlineData('S', "/sales")]
    [InlineData('R', "/reports")]
    [InlineData('G', "/settings")]
    public void Alt_letter_opens_the_screen_from_anywhere(char key, string expected)
    {
        Assert.Equal(expected, Shortcuts.Target("/sale/abc", key, shift: false));
        Assert.Equal(expected, Shortcuts.Target("/", char.ToLowerInvariant(key), shift: false));
    }

    [Theory]
    [InlineData('S', "/sale")]
    [InlineData('P', "/purchase")]
    [InlineData('O', "/salesorder")]
    [InlineData('C', "/customer")]
    [InlineData('U', "/supplier")]
    [InlineData('I', "/product")]
    public void Alt_shift_letter_opens_a_new_one(char key, string expected)
        => Assert.Equal(expected, Shortcuts.Target("/reports", key, shift: true));

    [Fact]
    public void Alt_shift_does_nothing_on_screens_without_a_new_form()
    {
        Assert.Null(Shortcuts.Target("/", 'R', shift: true));
        Assert.Null(Shortcuts.Target("/", 'G', shift: true));
    }

    [Theory]
    [InlineData("/sales", "/sale")]
    [InlineData("/sale/1b2c", "/sale")]
    [InlineData("/salessummary/1b2c", "/sale")]
    [InlineData("/purchases", "/purchase")]
    [InlineData("/purchase/1b2c?x=1", "/purchase")]
    [InlineData("/salesorders", "/salesorder")]
    [InlineData("/customer/7", "/customer")]
    [InlineData("/products", "/product")]
    public void Alt_n_starts_a_new_one_of_the_current_screen(string current, string expected)
        => Assert.Equal(expected, Shortcuts.Target(current, 'N', shift: false));

    [Theory]
    [InlineData("/")]
    [InlineData("/reports")]
    [InlineData("/settings")]
    [InlineData("/changepassword")]
    public void Alt_n_does_nothing_where_there_is_nothing_new_to_start(string current)
        => Assert.Null(Shortcuts.Target(current, 'N', shift: false));

    [Fact]
    public void Same_screen_is_reloaded_so_a_new_form_starts_empty()
    {
        Assert.True(Shortcuts.NeedsReload("/sale/1b2c", "/sale"));
        Assert.True(Shortcuts.NeedsReload("/sale", "/sale"));
        Assert.False(Shortcuts.NeedsReload("/sales", "/sale"));
        Assert.False(Shortcuts.NeedsReload("/", "/customers"));
    }

    [Theory]
    [InlineData("/customer", "/customers")]
    [InlineData("/customer/7", "/customers")]
    [InlineData("/supplier", "/suppliers")]
    [InlineData("/product/7", "/products")]
    [InlineData("/category", "/products")]
    [InlineData("/purchase", "/purchases")]
    [InlineData("/purchase/1b2c", "/purchases")]
    [InlineData("/sale/1b2c", "/sales")]
    [InlineData("/salessummary/1b2c", "/sales")]
    [InlineData("/salesorder", "/salesorders")]
    [InlineData("/customers", "/")]
    [InlineData("/sales", "/")]
    [InlineData("/purchaseorder", "/")]
    [InlineData("/reports", "/")]
    [InlineData("/settings", "/")]
    [InlineData("/changepassword", "/")]
    public void Esc_goes_back_to_the_list_then_to_the_dashboard(string current, string expected)
        => Assert.Equal(expected, Shortcuts.Back(current));

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/login?returnUrl=%2Fsales")]
    public void Esc_does_nothing_on_the_dashboard_or_login(string current)
        => Assert.Null(Shortcuts.Back(current));

    [Fact]
    public void Keys_are_unique_and_avoid_the_ones_chrome_needs()
    {
        var keys = Shortcuts.Screens.Select(s => s.Key).Append(Shortcuts.NewForThisScreen).Append(Shortcuts.BulkEntry).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain('F', keys);   // Chrome menu
        Assert.DoesNotContain('E', keys);   // Chrome menu
        Assert.DoesNotContain('D', keys);   // address bar
        Assert.Equal("Alt+C", Shortcuts.HintFor("/customers"));
    }
}
