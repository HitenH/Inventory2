using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain;

/// <summary>Loads display names for transaction lines without EF tracking conflicts.</summary>
public static class TransactionLineHydrator
{
    public static async Task HydratePurchaseAsync(AppDbContext context, PurchaseEntity purchase)
    {
        if (purchase.Supplier is null || purchase.Supplier.Id == Guid.Empty)
        {
            purchase.Supplier = await context.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == purchase.SupplierEntityId);
        }

        var lines = purchase.PurchaseVariants;
        if (lines is null || lines.Count == 0)
        {
            return;
        }

        var (products, variants) = await LoadLookupsAsync(
            context,
            lines.Select(l => l.ProductEntityId),
            lines.Select(l => l.VariantEntityId));

        foreach (var line in lines)
        {
            if (products.TryGetValue(line.ProductEntityId, out var product))
            {
                line.Product = product;
            }

            if (variants.TryGetValue(line.VariantEntityId, out var variant))
            {
                line.ProductVariant = variant;
            }
        }
    }

    public static async Task HydrateSalesOrderAsync(AppDbContext context, SalesOrderEntity order)
    {
        if (order.Customer is null || order.Customer.Id == Guid.Empty)
        {
            order.Customer = await context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == order.CustomerEntityId);
        }

        var lines = order.SalesOrderVariants;
        if (lines is null || lines.Count == 0)
        {
            return;
        }

        var (products, variants) = await LoadLookupsAsync(
            context,
            lines.Select(l => l.ProductEntityId),
            lines.Select(l => l.VariantEntityId));

        foreach (var line in lines)
        {
            if (products.TryGetValue(line.ProductEntityId, out var product))
            {
                line.Product = product;
            }

            if (variants.TryGetValue(line.VariantEntityId, out var variant))
            {
                line.ProductVariant = variant;
            }
        }
    }

    private static async Task<(Dictionary<Guid, ProductEntity> Products, Dictionary<Guid, VariantEntity> Variants)> LoadLookupsAsync(
        AppDbContext context,
        IEnumerable<Guid> productIds,
        IEnumerable<Guid> variantIds)
    {
        var productIdList = productIds.Distinct().ToList();
        var variantIdList = variantIds.Distinct().ToList();

        var products = productIdList.Count == 0
            ? new Dictionary<Guid, ProductEntity>()
            : await context.Products.AsNoTracking()
                .Where(p => productIdList.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

        var variants = variantIdList.Count == 0
            ? new Dictionary<Guid, VariantEntity>()
            : await context.Variants.AsNoTracking()
                .Where(v => variantIdList.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

        return (products, variants);
    }
}
