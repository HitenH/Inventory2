namespace Inventory.Service;

/// <summary>Shared line total math for purchase/sales order line entry.</summary>
public static class LineAmounts
{
    public static decimal ComputeAmount(decimal? rate, int? quantity)
    {
        if (rate is null or 0 || quantity is null or 0)
        {
            return 0;
        }

        return Math.Round(rate.Value * quantity.Value, 2);
    }

    public static decimal? ComputeAmountAfterDiscount(decimal? amount, int? discountPercent)
    {
        if (amount is null or 0)
        {
            return amount;
        }

        if (discountPercent is null or 0)
        {
            return Math.Round(amount.Value, 2);
        }

        var discounted = amount - amount * (discountPercent.Value / 100m);
        return Math.Round(discounted.Value, 2);
    }
}
