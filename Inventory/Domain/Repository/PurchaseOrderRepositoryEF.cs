using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class PurchaseOrderRepositoryEF : SimpleRepositoryEF<PurchaseOrderEntity>, IPurchaseOrderRepository
    {
        public PurchaseOrderRepositoryEF(IDbContextFactory<AppDbContext> factory) : base(factory, x => x.Id) { }
    }
}
