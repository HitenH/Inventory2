using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class PurchaseOrderRepositoryEF : IPurchaseOrderRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public PurchaseOrderRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(PurchaseOrderEntity item)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.PurchaseOrders.AddAsync(item);
            await context.SaveChangesAsync();
        }

        public async Task Delete(PurchaseOrderEntity item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.PurchaseOrders.FindAsync(item.Id);
            if (existing is null)
                return;

            context.PurchaseOrders.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<PurchaseOrderEntity> items)
        {
            await using var context = await factory.CreateDbContextAsync();
            var ids = items.Select(i => i.Id).ToList();
            var existing = await context.PurchaseOrders.Where(p => ids.Contains(p.Id)).ToListAsync();
            context.PurchaseOrders.RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseOrderEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.PurchaseOrders.AsNoTracking().ToListAsync();
        }

        public async Task<PurchaseOrderEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>Loads the row, copies the changed values onto it, saves.</summary>
        public async Task Update(PurchaseOrderEntity item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.PurchaseOrders.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Entry(existing).CurrentValues.SetValues(item);
            await context.SaveChangesAsync();
        }
    }
}
