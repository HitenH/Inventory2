using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class PurchaseVariantRepositoryEF : IPurchaseVariantRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public PurchaseVariantRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(PurchaseVariant variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var entity = new PurchaseVariant
            {
                PurchaseEntityId = variant.PurchaseEntityId,
                ProductEntityId = variant.ProductEntityId,
                VariantEntityId = variant.VariantEntityId,
                SerialNumber = variant.SerialNumber,
                Quantity = variant.Quantity,
                ProductRate = variant.ProductRate,
                Amount = variant.Amount,
                Discount = variant.Discount,
                AmountAfterDiscount = variant.AmountAfterDiscount
            };

            await context.PurchaseVariant.AddAsync(entity);
            await SaveWithTotalAsync(context, entity.PurchaseEntityId);
        }

        public async Task Delete(PurchaseVariant variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.PurchaseVariant.FindAsync(variant.Id);
            if (existing is null)
            {
                return;
            }

            context.PurchaseVariant.Remove(existing);
            await SaveWithTotalAsync(context, existing.PurchaseEntityId);
        }

        public async Task<List<PurchaseVariant>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.PurchaseVariant.AsNoTracking().ToListAsync();
        }

        public async Task<PurchaseVariant> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.PurchaseVariant
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id) ?? new PurchaseVariant();
        }

        public async Task Update(PurchaseVariant variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.PurchaseVariant.FindAsync(variant.Id);
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

            await SaveWithTotalAsync(context, existing.PurchaseEntityId);
        }

        /// <summary>Batch 7: saves the line change and the recalculated purchase total in one transaction (all or nothing).</summary>
        private static async Task SaveWithTotalAsync(AppDbContext context, Guid headerId)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.SaveChangesAsync();
            await TransactionTotals.RecalculatePurchaseAsync(context, headerId);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }
}
