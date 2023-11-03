using Inventory.Domain.Entities;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository.Abstract
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
            await context.PurchaseVariant.AddAsync(variant);
            await context.SaveChangesAsync();
        }

        public async Task Delete(PurchaseVariant variant)
        {
            context.PurchaseVariant.Remove(variant);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseVariant>> GetAll()
        {
            return await context.PurchaseVariant.AsNoTracking().ToListAsync();
        }

        public async Task<PurchaseVariant> GetById(Guid id)
        {
            return await context.PurchaseVariant.FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(PurchaseVariant variant)
        {
            context.PurchaseVariant.Update(variant);
            await context.SaveChangesAsync();
        }
    }
}
