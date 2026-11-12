using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class ProductRepositoryEF : IProductRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public ProductRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task<Guid> Create(ProductEntity product)
        {
            await using var context = await factory.CreateDbContextAsync();
            // The category was loaded by another (closed) context: tell this context it already exists,
            // otherwise Add would try to insert it again.
            if (product.Category != null)
                context.Categories.Attach(product.Category);

            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
            return product.Id;
        }

        public async Task Delete(ProductEntity product)
        {
            await using var context = await factory.CreateDbContextAsync();
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
                    var existing = await context.Products
                        .Include(p => p.Variants!).ThenInclude(v => v.Image)
                        .FirstOrDefaultAsync(p => p.Id == product.Id);
                    if (existing != null)
                    {
                        context.Products.Remove(existing);   // variants and photos are removed with it (cascade)
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<List<ProductEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
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
            await using var context = await factory.CreateDbContextAsync();
            return await context.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<List<ProductReportItem>> GetProductStockReportAsync(bool onlyBelowReorderLevel = false)
        {
            await using var context = await factory.CreateDbContextAsync();
            var (purchaseQtyByVariant, salesQtyByVariant) = await GetPurchasedAndSoldAsync(context, variantIds: null);

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
            await using var context = await factory.CreateDbContextAsync();
            var idList = variantIds.Distinct().ToList();
            if (idList.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var (purchaseQtyByVariant, salesQtyByVariant) = await GetPurchasedAndSoldAsync(context, idList);

            return idList.ToDictionary(
                id => id,
                id => purchaseQtyByVariant.GetValueOrDefault(id) - salesQtyByVariant.GetValueOrDefault(id));
        }

        /// <summary>
        /// The one place stock is worked out: total purchased and total sold per variant.
        /// Stock in hand = purchased - sold. Pass null to get every variant.
        /// </summary>
        private static async Task<(Dictionary<Guid, int> Purchased, Dictionary<Guid, int> Sold)> GetPurchasedAndSoldAsync(
            AppDbContext context, List<Guid>? variantIds)
        {
            var purchaseLines = context.PurchaseVariant.AsNoTracking();
            var salesLines = context.SalesVariants.AsNoTracking();
            if (variantIds is not null)
            {
                purchaseLines = purchaseLines.Where(pv => variantIds.Contains(pv.VariantEntityId));
                salesLines = salesLines.Where(sv => variantIds.Contains(sv.VariantEntityId));
            }

            var purchased = await purchaseLines
                .GroupBy(pv => pv.VariantEntityId)
                .Select(g => new { VariantEntityId = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.VariantEntityId, x => x.Qty);

            var sold = await salesLines
                .GroupBy(sv => sv.VariantEntityId)
                .Select(g => new { VariantEntityId = g.Key, Qty = g.Sum(x => x.Quantity ?? 0) })
                .ToDictionaryAsync(x => x.VariantEntityId, x => x.Qty);

            return (purchased, sold);
        }

        public async Task<ProductEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            // WithIdentityResolution keeps the same object shape as the old tracked query
            // (each variant's Product points back to this product).
            return await context.Products
                .AsNoTrackingWithIdentityResolution()
                .AsSplitQuery()
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .ThenInclude(v => v.Image)
                .FirstOrDefaultAsync(c => c.Id == id, default) ?? new ProductEntity();
        }

        public async Task<ProductEntity> GetByProductId(string productId)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Products
                .AsNoTracking()
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(c => c.ProductId == productId, default) ?? new ProductEntity();
        }

        public async Task Update(ProductEntity product)
        {
            await using var context = await factory.CreateDbContextAsync();
            // Batch 6: load the product fresh, copy what the screen changed, and save only those rows.
            // The screen passes the product with its variants (Variant.razor adds or edits one variant and
            // calls this), so variants and their photos are synced here too. Variants missing from the list
            // are NOT deleted here – deleting a variant goes through VariantRepository.Delete.
            var existing = await context.Products
                .Include(p => p.Category)
                .Include(p => p.Variants!).ThenInclude(v => v.Image)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == product.Id);
            if (existing is null)
                return;

            existing.ProductId = product.ProductId;
            existing.Name = product.Name;
            existing.Rate = product.Rate;
            existing.Description = product.Description;
            existing.Category = product.Category is null
                ? null
                : await context.Categories.FindAsync(product.Category.Id);

            existing.Variants ??= new List<VariantEntity>();
            var replacedImages = new List<Image>();
            var newImages = new List<Image>();

            foreach (var incoming in product.Variants ?? new List<VariantEntity>())
            {
                var current = incoming.Id == Guid.Empty ? null : existing.Variants.FirstOrDefault(v => v.Id == incoming.Id);
                if (current is null)
                {
                    // New variant. Give it its ID now, and write the ID back to the screen's object,
                    // so a later edit of the same variant finds it.
                    if (incoming.Id == Guid.Empty)
                        incoming.Id = Guid.NewGuid();

                    var added = new VariantEntity
                    {
                        Id = incoming.Id,
                        ProductEntityId = existing.Id,
                        VariantId = incoming.VariantId,
                        Name = incoming.Name,
                        ReorderLevel = incoming.ReorderLevel
                    };
                    context.Variants.Add(added);

                    if (incoming.Image?.ImageData != null)
                        newImages.Add(NewImageFor(incoming, added.Id));
                    continue;
                }

                current.VariantId = incoming.VariantId;
                current.Name = incoming.Name;
                current.ReorderLevel = incoming.ReorderLevel;

                var image = incoming.Image;
                if (image is null)
                    continue;   // no photo chosen: keep whatever is stored

                if (current.Image != null && current.Image.Id == image.Id)
                {
                    current.Image.ImageTitle = image.ImageTitle;   // same photo: update in place if changed
                    current.Image.ImageData = image.ImageData;
                }
                else if (image.ImageData != null)
                {
                    // A different photo: the old one is deleted (it used to be left behind as an orphan row).
                    if (current.Image != null)
                        replacedImages.Add(current.Image);
                    newImages.Add(NewImageFor(incoming, current.Id));
                }
            }

            // Delete replaced photos first, then add new ones, in one transaction
            // (one photo per variant is enforced by a unique index).
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Images.RemoveRange(replacedImages);
            await context.SaveChangesAsync();
            context.Images.AddRange(newImages);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private static Image NewImageFor(VariantEntity incoming, Guid variantId)
        {
            var image = incoming.Image!;
            if (image.Id == Guid.Empty)
                image.Id = Guid.NewGuid();
            image.VariantEntityId = variantId;   // write back to the screen's object as well
            return new Image
            {
                Id = image.Id,
                ImageTitle = image.ImageTitle,
                ImageData = image.ImageData,
                VariantEntityId = variantId
            };
        }

        public bool IsProductIdExist(string productId)
        {
            using var context = factory.CreateDbContext();
            return context.Products.Any(c => c.ProductId!.ToLower() == productId.Trim().ToLower());
        }

        public bool IsProductIdExist(string productId, Guid id)
        {
            using var context = factory.CreateDbContext();
            var result = context.Products.Any(c => c.ProductId!.ToLower() == productId.Trim().ToLower() && c.Id == id);
            if (result)
                return false;
            else
                return IsProductIdExist(productId);
        }

        public async Task<ProductEntity> GetProductWithVariantsById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Products
                .AsNoTracking()
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(c => c.Id == id, default) ?? new ProductEntity();
        }
    }
}
