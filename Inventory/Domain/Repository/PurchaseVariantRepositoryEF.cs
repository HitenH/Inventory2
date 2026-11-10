using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class PurchaseVariantRepositoryEF : IPurchaseVariantRepository
    {
        private readonly AppDbContext context;

        public PurchaseVariantRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }

        public async Task Create(PurchaseVariant variant)
        {
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
            await context.SaveChangesAsync();
        }

        public async Task Delete(PurchaseVariant variant)
        {
            var existing = await context.PurchaseVariant.FindAsync(variant.Id);
            if (existing is null)
            {
                return;
            }

            context.PurchaseVariant.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseVariant>> GetAll()
        {
            return await context.PurchaseVariant.AsNoTracking().ToListAsync();
        }

        public async Task<PurchaseVariant> GetById(Guid id)
        {
            return await context.PurchaseVariant
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id) ?? new PurchaseVariant();
        }

        public async Task Update(PurchaseVariant variant)
        {
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

            await context.SaveChangesAsync();
        }
    }
}
