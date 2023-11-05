using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class SalesOrderVariantRepositoryEF : ISalesOrderVariantRepository
    {
        private readonly AppDbContext context;

        public SalesOrderVariantRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(SalesOrderVariantEntity variant)
        {
            await context.SalesOrderVariants.AddAsync(variant);
            await context.SaveChangesAsync();
        }

        public async Task Delete(SalesOrderVariantEntity variant)
        {
            context.SalesOrderVariants.Remove(variant);
            await context.SaveChangesAsync();
        }

        public async Task<List<SalesOrderVariantEntity>> GetAll()
        {
            return await context.SalesOrderVariants.AsNoTracking().ToListAsync();
        }

        public async Task<SalesOrderVariantEntity> GetById(Guid id)
        {
            return await context.SalesOrderVariants.Include(p => p.Product).ThenInclude(p => p.Variants).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SalesOrderVariantEntity variant)
        {
            context.SalesOrderVariants.Update(variant);
            await context.SaveChangesAsync();
        }
    }
}
