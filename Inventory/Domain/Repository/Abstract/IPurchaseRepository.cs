using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseRepository
    {
        Task Create(PurchaseEntity purchase);
        Task<PurchaseEntity> GetById(Guid id);
        Task<List<PurchaseEntity>> GetAll();
        Task Update(PurchaseEntity purchase);
        Task Delete(PurchaseEntity purchase);
    }
}
