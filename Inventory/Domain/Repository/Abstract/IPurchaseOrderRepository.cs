using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseOrderRepository
    {
        Task Create(PurchaseOrderEntity model);
        /// <summary>Bulk entry: adds all rows in one save (all or nothing).</summary>
        Task CreateRange(List<PurchaseOrderEntity> models);
        Task<PurchaseOrderEntity> GetById(Guid id);
        Task<List<PurchaseOrderEntity>> GetAll();
        Task Update(PurchaseOrderEntity model);
        Task Delete(PurchaseOrderEntity model);
        Task DeleteRange(List<PurchaseOrderEntity> models);
        /// <summary>Sets the status of all the given orders in one save (owner's request, Nov 2026). Returns how many changed.</summary>
        Task<int> UpdateStatus(IReadOnlyCollection<Guid> ids, Inventory.Models.OrderStatus status);
    }
}
