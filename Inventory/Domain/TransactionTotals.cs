using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain;

/// <summary>
/// Batch 7: keeps the stored header total of purchases and sales orders in step with their lines.
/// Total = sum of the lines' "amount after discount" minus the header discount (same formula as the screens).
/// Call it inside the same transaction as the change to the lines or header, then SaveChanges.
/// </summary>
public static class TransactionTotals
{
    public static async Task RecalculatePurchaseAsync(AppDbContext context, Guid purchaseId)
    {
        // Returns the already-tracked header if this context loaded it, so pending header changes (discount) count.
        var header = await context.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId);
        if (header is null)
            return;

        var amounts = await context.PurchaseVariant
            .Where(l => l.PurchaseEntityId == purchaseId)
            .Select(l => l.AmountAfterDiscount)
            .ToListAsync();

        header.TotalAmountProduct = amounts.Sum(a => a ?? 0) - (header.Discount ?? 0);
    }

    public static async Task RecalculateSalesOrderAsync(AppDbContext context, Guid salesOrderId)
    {
        var header = await context.SalesOrders.FirstOrDefaultAsync(o => o.Id == salesOrderId);
        if (header is null)
            return;

        var amounts = await context.SalesOrderVariants
            .Where(l => l.SalesOrderEntityId == salesOrderId)
            .Select(l => l.AmountAfterDiscount)
            .ToListAsync();

        header.TotalAmountProduct = amounts.Sum(a => a ?? 0) - (header.Discount ?? 0);
    }
}
