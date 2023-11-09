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
                Console.WriteLine(ex.Message);
            }
            
        }

        public async Task Delete(ProductEntity product)
        {
            context.Products.Remove(product);
            await context.SaveChangesAsync();
        }

        public async Task<List<ProductEntity>> GetAll()
        {
            return await context.Products.Include(p => p.Variants).ThenInclude(p => p.PurchaseVariants).ToListAsync();
        }

        public async Task<ProductEntity> GetById(Guid id)
        {
            return await context.Products.Include(p => p.Category).Include(p => p.Variants).ThenInclude(v=>v.Image).Include(p => p.PurchaseVariants).FirstOrDefaultAsync(c => c.Id == id, default);
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
    }
}
