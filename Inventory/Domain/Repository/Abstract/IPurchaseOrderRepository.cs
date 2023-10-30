using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseOrderRepository
    {
        Task Create(PurchaseOrder model);
        Task<PurchaseOrder> GetById(Guid id);
        Task<List<PurchaseOrder>> GetAll();
        Task Update(PurchaseOrder model);
        Task Delete(PurchaseOrder model);
        Task DeleteRange(List<PurchaseOrder> models);
    }
}
