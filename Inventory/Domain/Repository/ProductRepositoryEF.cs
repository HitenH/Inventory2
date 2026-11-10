using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class ProductRepositoryEF : IProductRepository
    {
        private readonly AppDbContext context;

        public ProductRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<Guid> Create(ProductEntity product)
        {
            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
            return product.Id;
        }

        public async Task Delete(ProductEntity product)
        {
            try
            {
                var hasRelatedSalesVariant = context.SalesVariants.Any(s => s.ProductEntityId == product.Id);
                var hasRelatedPurchases = context.PurchaseVariant.Any(p => p.ProductEntityId == product.Id);
                var hasRelatedSalesOrder = context.SalesOrderVariants.Any(p => p.ProductEntityId == product.Id);
                if (hasRelatedSalesVariant || hasRelatedPurchases || hasRelatedSalesOrder)
                {
                    throw new Exception("The Product cannot be deleted due to association with other entities");
                }
                else
                {
                    context.Products.Remove(product);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<List<ProductEntity>> GetAll()
        {
            return await context.Products
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<List<ProductEntity>> GetAllForLookup()
        {
            return await context.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<List<ProductReportItem>> GetProductStockReportAsync(bool onlyBelowReorderLevel = false)
        {
            var purchaseQtyByVariant = await context.PurchaseVariant
                .AsNoTracking()
                .GroupBy(pv => pv.VariantEntityId)
                .Select(g => new { VariantEntityId = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.VariantEntityId, x => x.Qty);

            var salesQtyByVariant = await context.SalesVariants
                .AsNoTracking()
                .GroupBy(sv => sv.VariantEntityId)
                .Select(g => new { VariantEntityId = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.VariantEntityId, x => x.Qty);

            var variants = await context.Variants
                .AsNoTracking()
                .Include(v => v.Product)
                .OrderBy(v => v.Product!.Name)
                .ThenBy(v => v.VariantId)
                .ToListAsync();

            var reportItems = new List<ProductReportItem>();

            foreach (var variant in variants)
            {
                var product = variant.Product;
                if (product is null)
                {
                    continue;
                }

                purchaseQtyByVariant.TryGetValue(variant.Id, out var purchasedQty);
                salesQtyByVariant.TryGetValue(variant.Id, out var soldQty);
                var stockInHand = purchasedQty - soldQty;

                if (onlyBelowReorderLevel && stockInHand > (variant.ReorderLevel ?? int.MaxValue))
                {
                    continue;
                }

                reportItems.Add(new ProductReportItem
                {
                    ProductId = product.ProductId ?? string.Empty,
                    ProductName = product.Name ?? string.Empty,
                    VariantId = variant.VariantId,
                    VariantName = variant.Name ?? string.Empty,
                    StockInHand = stockInHand,
                    ReorderLevel = variant.ReorderLevel,
                    SalesQuantity = soldQty,
                    SalesAmount = soldQty * product.Rate
                });
            }

            return reportItems;
        }

        public async Task<Dictionary<Guid, int>> GetStockInHandByVariantIdsAsync(IEnumerable<Guid> variantIds)
        {
            var idList = variantIds.Distinct().ToList();
            if (idList.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var purchaseQtyByVariant = await context.PurchaseVariant
                .AsNoTracking()
                .Where(pv => idList.Contains(pv.VariantEntityId))
                .GroupBy(pv => pv.VariantEntityId)
                .Select(g => new { VariantEntityId = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.VariantEntityId, x => x.Qty);

            var salesQtyByVariant = await context.SalesVariants
                .AsNoTracking()
                .Where(sv => idList.Contains(sv.VariantEntityId))
                .GroupBy(sv => sv.VariantEntityId)
                .Select(g => new { VariantEntityId = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.VariantEntityId, x => x.Qty);

            return idList.ToDictionary(
                id => id,
                id => purchaseQtyByVariant.GetValueOrDefault(id) - salesQtyByVariant.GetValueOrDefault(id));
        }

        public async Task<ProductEntity> GetById(Guid id)
        {
            return await context.Products
                .AsSplitQuery()
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Image)
                .FirstOrDefaultAsync(c => c.Id == id, default) ?? new ProductEntity();
        }

        public async Task<ProductEntity> GetByProductId(string productId)
        {
            return await context.Products
                .AsNoTracking()
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(c => c.ProductId == productId, default) ?? new ProductEntity();
        }

        public async Task Update(ProductEntity product)
        {
            context.Products.Update(product);
            await context.SaveChangesAsync();
        }

        public bool IsProductIdExist(string productId)
        {
            return context.Products.Any(c => c.ProductId == productId);
        }

        public bool IsProductIdExist(string productId, Guid id)
        {
            var result = context.Products.Any(c => c.ProductId == productId && c.Id == id);
            if (result)
                return false;
            else
                return IsProductIdExist(productId);
        }

        public async Task<ProductEntity> GetProductWithVariantsById(Guid id)
        {
            return await context.Products
                .AsNoTracking()
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(c => c.Id == id, default) ?? new ProductEntity();
        }
    }
}
