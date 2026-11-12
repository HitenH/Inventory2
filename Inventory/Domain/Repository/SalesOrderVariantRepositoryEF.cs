using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class SalesOrderVariantRepositoryEF : ISalesOrderVariantRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public SalesOrderVariantRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(SalesOrderVariantEntity variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var entity = new SalesOrderVariantEntity
            {
                SalesOrderEntityId = variant.SalesOrderEntityId,
                ProductEntityId = variant.ProductEntityId,
                VariantEntityId = variant.VariantEntityId,
                SerialNumber = variant.SerialNumber,
                Quantity = variant.Quantity,
                ProductRate = variant.ProductRate,
                Amount = variant.Amount,
                Discount = variant.Discount,
                AmountAfterDiscount = variant.AmountAfterDiscount,
                Remarks = variant.Remarks
            };

            await context.SalesOrderVariants.AddAsync(entity);
            await SaveWithTotalAsync(context, entity.SalesOrderEntityId);
        }

        public async Task Delete(SalesOrderVariantEntity variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.SalesOrderVariants.FindAsync(variant.Id);
            if (existing is null)
            {
                return;
            }

            context.SalesOrderVariants.Remove(existing);
            await SaveWithTotalAsync(context, existing.SalesOrderEntityId);
        }

        public async Task<List<SalesOrderVariantEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.SalesOrderVariants.AsNoTracking().ToListAsync();
        }

        public async Task<SalesOrderVariantEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.SalesOrderVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id) ?? new SalesOrderVariantEntity();
        }

        public async Task Update(SalesOrderVariantEntity variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.SalesOrderVariants.FindAsync(variant.Id);
            if (existing is null)
            {
                return;
            }

            existing.SerialNumber = variant.SerialNumber;
            existing.ProductEntityId = variant.ProductEntityId;
            existing.VariantEntityId = variant.VariantEntityId;
            existing.Quantity = variant.Quantity;
            existing.ProductRate = variant.ProductRate;
            existing.Amount = variant.Amount;
            existing.Discount = variant.Discount;
            existing.AmountAfterDiscount = variant.AmountAfterDiscount;
            existing.Remarks = variant.Remarks;

            await SaveWithTotalAsync(context, existing.SalesOrderEntityId);
        }

        /// <summary>Batch 7: saves the line change and the recalculated sales order total in one transaction (all or nothing).</summary>
        private static async Task SaveWithTotalAsync(AppDbContext context, Guid headerId)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.SaveChangesAsync();
            await TransactionTotals.RecalculateSalesOrderAsync(context, headerId);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }
}
