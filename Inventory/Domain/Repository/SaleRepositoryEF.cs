using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class SaleRepositoryEF : ISaleRepository
    {
        public const string EmptySaleMessage = "A sale must have at least one item. To cancel the whole sale, use Delete.";

        private readonly IDbContextFactory<AppDbContext> factory;

        public SaleRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task<Guid> Create(SalesEntity sale)
        {
            await using var context = await factory.CreateDbContextAsync();
            var customerId = sale.CustomerEntityId != Guid.Empty
                ? sale.CustomerEntityId
                : sale.Customer?.Id ?? Guid.Empty;

            var salesEntity = new SalesEntity
            {
                CustomerEntityId = customerId,
                Date = sale.Date,
                VoucherId = sale.VoucherId,
                Remarks = sale.Remarks,
                SalesVariants = sale.SalesVariants?.Select(v => new SalesVariantEntity
                {
                    ProductEntityId = v.ProductEntityId,
                    VariantEntityId = v.VariantEntityId,
                    Quantity = v.Quantity
                }).ToList() ?? new()
            };

            await context.Sales.AddAsync(salesEntity);
            await context.SaveChangesAsync();
            return salesEntity.Id;
        }

        public async Task Delete(SalesEntity sale)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Sales
                .Include(s => s.SalesVariants)
                .Include(s => s.SalesSummaries)
                .FirstOrDefaultAsync(s => s.Id == sale.Id);

            if (existing is null)
            {
                return;
            }

            context.Sales.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<SalesEntity> sales)
        {
            await using var context = await factory.CreateDbContextAsync();
            // Load by ID in this context (the list came from another, already closed context).
            var ids = sales.Select(s => s.Id).ToList();
            var existing = await context.Sales
                .Include(s => s.SalesVariants)
                .Include(s => s.SalesSummaries)
                .Where(s => ids.Contains(s.Id))
                .ToListAsync();
            context.Sales.RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<SalesEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Sales
                .AsNoTracking()
                .Include(c => c.Customer)
                .ToListAsync();
        }

        public async Task<SalesEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            var sale = await context.Sales
                .AsNoTracking()
                .Include(s => s.SalesSummaries)
                .Include(s => s.Customer)
                .Include(s => s.SalesVariants)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (sale is null)
            {
                return new SalesEntity();
            }

            await HydrateSaleDetailsAsync(context, sale);
            return sale;
        }

        public async Task Update(SalesEntity sale)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Sales
                .Include(s => s.SalesVariants)
                .Include(s => s.SalesSummaries)
                .FirstOrDefaultAsync(s => s.Id == sale.Id);

            if (existing is null)
            {
                return;
            }

            existing.VoucherId = sale.VoucherId;
            existing.Date = sale.Date;
            existing.CustomerEntityId = sale.CustomerEntityId != Guid.Empty
                ? sale.CustomerEntityId
                : sale.Customer?.Id ?? existing.CustomerEntityId;
            existing.Remarks = sale.Remarks;
            existing.Discount = sale.Discount;
            existing.TotalQuantity = sale.TotalQuantity;
            existing.TotalAmountProduct = sale.TotalAmountProduct;

            // Batch 7: removing every item is refused (it used to keep the old items silently).
            // A null list means "items not being changed" (e.g. saving the summary) and leaves them as they are.
            if (sale.SalesVariants is { Count: 0 } && existing.SalesVariants is { Count: > 0 })
            {
                throw new InvalidOperationException(EmptySaleMessage);
            }

            if (sale.SalesVariants is { Count: > 0 })
            {
                context.SalesVariants.RemoveRange(existing.SalesVariants ?? new List<SalesVariantEntity>());
                existing.SalesVariants = sale.SalesVariants.Select(v => new SalesVariantEntity
                {
                    SalesEntityId = existing.Id,
                    ProductEntityId = v.ProductEntityId,
                    VariantEntityId = v.VariantEntityId,
                    Quantity = v.Quantity
                }).ToList();
            }

            if (sale.SalesSummaries is not null)
            {
                context.SalesSummary.RemoveRange(existing.SalesSummaries ?? new List<SalesSummaryEntity>());
                existing.SalesSummaries = sale.SalesSummaries.Select(s => new SalesSummaryEntity
                {
                    SalesEntityId = existing.Id,
                    SerialNumber = s.SerialNumber,
                    ProductEntityId = s.ProductEntityId,
                    ProductId = s.ProductId,
                    ProductName = s.ProductName,
                    Quantity = s.Quantity,
                    ProductRate = s.ProductRate,
                    Amount = s.Amount,
                    Discount = s.Discount,
                    AmountAfterDiscount = s.AmountAfterDiscount
                }).ToList();
            }

            await context.SaveChangesAsync();
        }

        public int GetLastVoucherIdByDate(DateOnly date)
        {
            using var context = factory.CreateDbContext();
            var order = context.Sales.OrderByDescending(p => p.VoucherId).FirstOrDefault(s => s.Date == date);
            if (order != null)
            {
                return order.VoucherId;
            }
            return 0;
        }

        public bool IsVoucherExistByDate(int voucherId, DateOnly date)
        {
            using var context = factory.CreateDbContext();
            return context.Sales.Any(p => p.VoucherId == voucherId && p.Date == date);
        }

        private static async Task HydrateSaleDetailsAsync(AppDbContext context, SalesEntity sale)
        {
            if (sale.Customer is null || sale.Customer.Id == Guid.Empty)
            {
                sale.Customer = await context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == sale.CustomerEntityId);
            }

            var variants = sale.SalesVariants ?? new List<SalesVariantEntity>();
            if (variants.Count == 0)
            {
                return;
            }

            var productIds = variants.Select(v => v.ProductEntityId).Distinct().ToList();
            var variantIds = variants.Select(v => v.VariantEntityId).Distinct().ToList();

            var products = await context.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var variantEntities = await context.Variants.AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            foreach (var line in variants)
            {
                if (products.TryGetValue(line.ProductEntityId, out var product))
                {
                    line.Product = product;
                }

                if (variantEntities.TryGetValue(line.VariantEntityId, out var variant))
                {
                    line.ProductVariant = variant;
                }
            }
        }
    }
}
