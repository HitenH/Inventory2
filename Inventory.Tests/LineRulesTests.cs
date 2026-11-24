using Inventory.Service;

namespace Inventory.Tests;

/// <summary>
/// Version 3.0, stage 1a: the one rule book for voucher lines.
/// These tests are the written form of the shop's rules, so a later change that loosens one fails here.
/// </summary>
public class LineRulesTests
{
    private static readonly Guid AProduct = Guid.NewGuid();
    private static readonly Guid AVariant = Guid.NewGuid();

    [Fact]
    public void A_complete_line_is_allowed()
    {
        Assert.Null(LineRules.WhatIsWrong(AProduct, AVariant, quantity: 5, discountPercent: 10));
        Assert.True(LineRules.IsAllowed(AProduct, AVariant, quantity: 5, discountPercent: 10));
    }

    [Fact]
    public void A_line_without_a_discount_is_allowed()
    {
        // Purchase orders have no discount column: null means "no discount", not "wrong".
        Assert.Null(LineRules.WhatIsWrong(AProduct, AVariant, quantity: 1, discountPercent: null));
    }

    [Fact]
    public void No_product_is_refused_first()
    {
        var message = LineRules.WhatIsWrong(Guid.Empty, null, quantity: null);
        Assert.Equal("Please select a product.", message);
    }

    [Fact]
    public void No_variant_is_refused()
    {
        Assert.Equal("Please select a variant.", LineRules.WhatIsWrong(AProduct, null, quantity: 3));
        Assert.Equal("Please select a variant.", LineRules.WhatIsWrong(AProduct, Guid.Empty, quantity: 3));
    }

    [Fact]
    public void A_missing_quantity_is_refused()
    {
        Assert.Equal("Please enter a quantity.", LineRules.WhatIsWrong(AProduct, AVariant, quantity: null));
    }

    [Fact]
    public void A_quantity_of_zero_is_refused()
    {
        Assert.Equal(LineRules.ZeroQuantityMessage, LineRules.WhatIsWrong(AProduct, AVariant, quantity: 0));
        Assert.False(LineRules.IsAllowed(AProduct, AVariant, quantity: 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-250)]
    public void Negative_quantities_are_allowed_for_returns_and_damage(int quantity)
    {
        Assert.Null(LineRules.WhatIsWrong(AProduct, AVariant, quantity));
        Assert.True(LineRules.IsAllowed(AProduct, AVariant, quantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void A_discount_from_0_to_100_is_allowed(int discount)
    {
        Assert.Null(LineRules.WhatIsWrong(AProduct, AVariant, quantity: 2, discountPercent: discount));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(500)]
    [InlineData(-1)]
    public void A_discount_outside_0_to_100_is_refused(int discount)
    {
        // A discount over 100 makes the line's amount negative, which quietly lowers the voucher
        // total and the supplier and report figures. Neither the line editor nor Bulk Entry
        // caught this before stage 1a.
        var message = LineRules.WhatIsWrong(AProduct, AVariant, quantity: 2, discountPercent: discount);
        Assert.Equal("Discount must be between 0 and 100 percent.", message);
    }

    [Fact]
    public void A_discount_over_100_would_make_the_amount_negative()
    {
        // Why the rule above exists, in the app's own maths.
        var amount = LineAmounts.ComputeAmount(rate: 10m, quantity: 2);
        var afterDiscount = LineAmounts.ComputeAmountAfterDiscount(amount, discountPercent: 500);
        Assert.True(afterDiscount < 0, "a discount over 100 turns the line amount negative");
        Assert.False(LineRules.IsAllowed(AProduct, AVariant, quantity: 2, discountPercent: 500));
    }

    [Fact]
    public void The_rule_book_and_Bulk_Entry_agree_about_quantity_and_product()
    {
        // Bulk Entry asks the rule book now; this guards against the two drifting apart again.
        foreach (var quantity in new int?[] { null, 0, -1, 1, 7 })
        {
            Assert.Equal(
                LineRules.WhatIsWrong(AProduct, AVariant, quantity),
                BulkEntry.WhatIsMissing(AProduct, AVariant, quantity));
        }

        Assert.Equal(
            LineRules.WhatIsWrong(Guid.Empty, null, null),
            BulkEntry.WhatIsMissing(Guid.Empty, null, null));
    }
}
