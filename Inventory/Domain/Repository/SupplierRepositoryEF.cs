using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
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
                var hasRelatedPurchases = context.Purchases.Any(p => p.SupplierEntityId == supplier.Id);  
                if (hasRelatedPurchases)
                {
                    throw new Exception("The Supplier cannot be deleted due to association with other entities");
                }
                else
                {
                    context.Suppliers.Remove(supplier);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
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

        public bool IsSupplierIdExist(string supplierId)
        {
            return context.Suppliers.Any(c => c.SupplierId == supplierId);
        }
        public bool IsSupplierIdExist(string supplierId, Guid id)
        {
            var result = context.Suppliers.Any(c => c.SupplierId == supplierId && c.Id == id);
            if (result)
                return false;
            else
                return IsSupplierIdExist(supplierId);
        }
    }
}
