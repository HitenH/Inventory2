using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class SaleRepositoryEF : ISaleRepository
    {
        private readonly AppDbContext context;

        public SaleRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }

        public async Task Create(SalesEntity sale)
        {
            await context.Sales.AddAsync(sale);
            await context.SaveChangesAsync();
        }

        public async Task Delete(SalesEntity sale)
        {
            context.Sales.Remove(sale);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<SalesEntity> sales)
        {
            context.Sales.RemoveRange(sales);
            await context.SaveChangesAsync();
        }

        public async Task<List<SalesEntity>> GetAll()
        {
            return await context.Sales.ToListAsync();
        }

        public async Task<SalesEntity> GetById(Guid id)
        {
            return await context.Sales.FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SalesEntity sale)
        {
            context.Sales.Update(sale);
            await context.SaveChangesAsync();
        }
    }
}
