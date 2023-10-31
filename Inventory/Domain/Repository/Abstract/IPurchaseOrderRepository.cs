using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseOrderRepository
    {
        Task Create(PurchaseOrderEntity model);
        Task<PurchaseOrderEntity> GetById(Guid id);
        Task<List<PurchaseOrderEntity>> GetAll();
        Task Update(PurchaseOrderEntity model);
        Task Delete(PurchaseOrderEntity model);
        Task DeleteRange(List<PurchaseOrderEntity> models);
    }
}
