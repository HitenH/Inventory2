using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class VariantRepositoryEF : IVariantRepository
    {
        private readonly AppDbContext context;

        public VariantRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(VariantEntity variant)
        {
            await context.Variants.AddAsync(variant);
            await context.SaveChangesAsync();
        }

        public async Task Delete(VariantEntity variant)
        {
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
                    context.Variants.Remove(variant);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
           
        }

        public async Task<List<VariantEntity>> GetAll()
        {
            return await context.Variants.Include(v => v.Image).AsNoTracking().ToListAsync();
        }

        public async Task<VariantEntity> GetById(Guid id)
        {
            return await context.Variants.Include(v=>v.Image).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(VariantEntity variant)
        {
            context.Variants.Update(variant);
            await context.SaveChangesAsync();
        }

        public bool IVariantIdExist(string variantId, Guid productEntityId)
        {
            return context.Variants.Any(c => c.VariantId == variantId && c.ProductEntityId == productEntityId);
        }
        public bool IVariantIdExist(string variantId, Guid productEntityId, Guid id)
        {
            var result = context.Variants.Any(c => c.VariantId == variantId && c.Id == id && c.ProductEntityId == productEntityId);
            if (result)
                return false;
            else
                return IVariantIdExist(variantId, productEntityId);
        }
    }
}
