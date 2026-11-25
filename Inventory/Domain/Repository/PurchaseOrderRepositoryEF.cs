using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class PurchaseOrderRepositoryEF : SimpleRepositoryEF<PurchaseOrderEntity>, IPurchaseOrderRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public PurchaseOrderRepositoryEF(IDbContextFactory<AppDbContext> factory) : base(factory, x => x.Id)
        {
            this.factory = factory;
        }

        /// <summary>Batch 1c-2: the Purchase Orders table is edited in place, so the rows it shows are the versions opened.</summary>
        protected override bool ListIsEditedInPlace => true;

        public async Task<int> UpdateStatus(IReadOnlyCollection<Guid> ids, Inventory.Models.OrderStatus status)
        {
            if (ids.Count == 0)
                return 0;
            await using var context = await factory.CreateDbContextAsync();
            var orders = await context.PurchaseOrders.Where(o => ids.Contains(o.Id)).ToListAsync();
            foreach (var order in orders)
            {
                order.OrderStatus = status;
            }
            await context.SaveChangesAsync();
            return orders.Count;
        }
    }
}
