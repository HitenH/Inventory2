using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
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
        public async Task Create(PurchaseOrderEntity model)
        {
            await context.PurchaseOrders.AddAsync(model);
            await context.SaveChangesAsync();
        }

        public async Task Delete(PurchaseOrderEntity model)
        {
            context.PurchaseOrders.Remove(model);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<PurchaseOrderEntity> models)
        {
            context.PurchaseOrders.RemoveRange(models);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseOrderEntity>> GetAll()
        {
            return await context.PurchaseOrders.ToListAsync();
        }

        public async Task<PurchaseOrderEntity> GetById(Guid id)
        {
            return await context.PurchaseOrders.FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(PurchaseOrderEntity model)
        {
            context.PurchaseOrders.Update(model);
            await context.SaveChangesAsync();
        }
    }
}
