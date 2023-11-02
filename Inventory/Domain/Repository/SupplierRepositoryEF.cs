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
            //await context.Suppliers.AddAsync(supplier);
            //await context.SaveChangesAsync();
        }

        public async Task Delete(SupplierEntity supplier)
        {
            //context.Suppliers.Remove(supplier);
            //await context.SaveChangesAsync();
        }

        public async Task<List<SupplierEntity>> GetAll()
        {
            return new();
            //return await context.Suppliers.Include(n => n.Mobiles).AsNoTracking().ToListAsync();
        }

        public async Task<SupplierEntity> GetById(Guid id)
        {
            return new();
            //return await context.Suppliers.Include(n => n.Mobiles).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SupplierEntity supplier)
        {
            //context.Suppliers.Update(supplier);
            //await context.SaveChangesAsync();
        }
    }
}
