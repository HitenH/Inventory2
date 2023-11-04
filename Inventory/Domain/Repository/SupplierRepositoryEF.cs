using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class SupplierRepositoryEF : ISupplierRepository
    {
        private readonly AppDbContext context;

        public SupplierRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }

        public async Task Create(SupplierEntity supplier)
        {
            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task Delete(SupplierEntity supplier)
        {
            try
            {
                context.Suppliers.Remove(supplier);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("delete supplier: " + ex.Message);
            }
           
        }

        public async Task<List<SupplierEntity>> GetAll()
        {
            return await context.Suppliers.Include(n => n.Mobiles).Include(s=>s.Purchases).AsNoTracking().ToListAsync();
        }

        public async Task<SupplierEntity> GetById(Guid id)
        {
           return await context.Suppliers.Include(n => n.Mobiles).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SupplierEntity supplier)
        {
            context.Suppliers.Update(supplier);
            await context.SaveChangesAsync();
        }
    }
}
