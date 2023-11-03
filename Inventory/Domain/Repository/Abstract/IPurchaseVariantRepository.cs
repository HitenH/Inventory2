using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseVariantRepository
    {
        Task Create(PurchaseVariant variant);
        Task<PurchaseVariant> GetById(Guid id);
        Task<List<PurchaseVariant>> GetAll();
        Task Update(PurchaseVariant variant);
        Task Delete(PurchaseVariant variant);
    }
}
