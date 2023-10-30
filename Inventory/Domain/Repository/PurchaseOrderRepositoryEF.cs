using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class PurchaseOrderRepositoryEF : IPurchaseOrderRepository
    {
        private readonly AppDbContext context;

        public PurchaseOrderRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(PurchaseOrder model)
        {
            await context.PurchaseOrders.AddAsync(model);
            await context.SaveChangesAsync();
        }

        public async Task Delete(PurchaseOrder model)
        {
            context.PurchaseOrders.Remove(model);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<PurchaseOrder> models)
        {
            context.PurchaseOrders.RemoveRange(models);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseOrder>> GetAll()
        {
            return await context.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Product).ToListAsync();
        }

        public async Task<PurchaseOrder> GetById(Guid id)
        {
            return await context.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Product).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(PurchaseOrder model)
        {
            context.PurchaseOrders.Update(model);
            await context.SaveChangesAsync();
        }
    }
}
