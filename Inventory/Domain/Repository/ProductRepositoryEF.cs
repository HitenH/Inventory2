using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
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

        public async Task Create(ProductEntity product)
        {
            try
            {
                await context.Products.AddAsync(product);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
            
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
            return await context.Products.Include(p => p.Variants).ThenInclude(p => p.PurchaseVariants).ToListAsync();
        }

        public async Task<ProductEntity> GetById(Guid id)
        {
            return await context.Products.Include(p => p.Category).Include(p => p.Variants).ThenInclude(v => v.Image).Include(p => p.PurchaseVariants).Include(p=>p.SalesVariants).FirstOrDefaultAsync(c => c.Id == id, default);
            //return await context.Products.Include(p => p.Category).Include(p => p.Variants).ThenInclude(v => v.Image).Include(p => p.PurchaseVariants).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task<ProductEntity> GetByProductId(string productId)
        {
            return await context.Products.Include(p => p.Variants).FirstOrDefaultAsync(c => c.ProductId == productId, default);
        }

        public async Task Update(ProductEntity product)
        {
            try
            {
                context.Products.Update(product);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
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
    }
}
