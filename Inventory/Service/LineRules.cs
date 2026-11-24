namespace Inventory.Service;

/// <summary>
/// Version 3.0, stage 1a: the one place that says what a valid voucher line is.
///
/// Before this, the same rule was written in three places and each knew a different amount of it:
/// the Bulk Entry pop-up refused quantity 0 but let a discount of 500 through; the line editor on
/// Purchase and Sales Order checked only that a product and variant were picked. The roadmap calls
/// for every screen to ask one rule book instead, because the shop will later run several devices
/// (checker's tablet, picker's phone, salesman's screen) and a rule copied into each one is a rule
/// that will eventually be forgotten in one of them.
///
/// Nothing here touches the database or a screen, so it can be tested on its own.
/// Keep it that way: if a new screen needs a line rule, add it here and call it from there.
/// </summary>
public static class LineRules
{
    /// <summary>The smallest quantity a line may carry. A line of nothing is not a line.</summary>
    public const int MinimumQuantity = 1;

    /// <summary>A discount is a whole percentage of the line's own amount.</summary>
    public const int MinimumDiscountPercent = 0;
    public const int MaximumDiscountPercent = 100;

    /// <summary>
    /// Why this line may not be saved, in words the operator can act on, or null when it is fine.
    /// The order matters: it names the first thing to fix, so the cursor can be sent there.
    /// </summary>
    /// <param name="productEntityId">The chosen product's internal id.</param>
    /// <param name="variantEntityId">The chosen variant's internal id.</param>
    /// <param name="quantity">How many pieces.</param>
    /// <param name="discountPercent">Line discount in percent, or null when the line has no discount
    /// (purchase orders have no discount column, so null is allowed and means "none").</param>
    public static string? WhatIsWrong(Guid productEntityId, Guid? variantEntityId, int? quantity, int? discountPercent = null)
    {
        if (productEntityId == Guid.Empty)
        {
            return "Please select a product.";
        }

        if (variantEntityId is null || variantEntityId == Guid.Empty)
        {
            return "Please select a variant.";
        }

        if (quantity is null)
        {
            return "Please enter a quantity.";
        }

        if (quantity < MinimumQuantity)
        {
            return $"Quantity must be at least {MinimumQuantity}.";
        }

        if (discountPercent is not null && (discountPercent < MinimumDiscountPercent || discountPercent > MaximumDiscountPercent))
        {
            return $"Discount must be between {MinimumDiscountPercent} and {MaximumDiscountPercent} percent.";
        }

        return null;
    }

    /// <summary>True when this line may be saved. <see cref="WhatIsWrong"/> says why not.</summary>
    public static bool IsAllowed(Guid productEntityId, Guid? variantEntityId, int? quantity, int? discountPercent = null)
        => WhatIsWrong(productEntityId, variantEntityId, quantity, discountPercent) is null;
}
