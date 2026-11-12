using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class VariantRepositoryEF : IVariantRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public VariantRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }
        public async Task Create(VariantEntity variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Variants.AddAsync(variant);
            await context.SaveChangesAsync();
        }

        public async Task Delete(VariantEntity variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            try
            {
                var hasRelatedPurchaseVariant = context.PurchaseVariant.Any(s => s.VariantEntityId == variant.Id);
                var hasRelatedSalesOrderVariants = context.SalesOrderVariants.Any(s => s.VariantEntityId == variant.Id);
                var hasRelatedSalesVariants = context.SalesVariants.Any(s => s.VariantEntityId == variant.Id);
                if (hasRelatedPurchaseVariant || hasRelatedSalesOrderVariants || hasRelatedSalesVariants)
                {
                    throw new Exception("The Product Variant cannot be deleted due to association with other entities");
                }
                else
                {
                    var existing = await context.Variants.Include(v => v.Image).FirstOrDefaultAsync(v => v.Id == variant.Id);
                    if (existing != null)
                    {
                        context.Variants.Remove(existing);   // its photo is removed with it (cascade)
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
           
        }

        public async Task<List<VariantEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Variants.Include(v => v.Image).AsNoTracking().ToListAsync();
        }

        public async Task<VariantEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Variants.AsNoTracking().Include(v=>v.Image).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(VariantEntity variant)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Variants.FindAsync(variant.Id);
            if (existing is null)
                return;

            existing.VariantId = variant.VariantId;
            existing.Name = variant.Name;
            existing.ReorderLevel = variant.ReorderLevel;
            await context.SaveChangesAsync();
        }

        public bool IVariantIdExist(string variantId, Guid productEntityId)
        {
            using var context = factory.CreateDbContext();
            return context.Variants.Any(c => c.VariantId!.ToLower() == variantId.Trim().ToLower() && c.ProductEntityId == productEntityId);
        }
        public bool IVariantIdExist(string variantId, Guid productEntityId, Guid id)
        {
            using var context = factory.CreateDbContext();
            var result = context.Variants.Any(c => c.VariantId!.ToLower() == variantId.Trim().ToLower() && c.Id == id && c.ProductEntityId == productEntityId);
            if (result)
                return false;
            else
                return IVariantIdExist(variantId, productEntityId);
        }
    }
}
