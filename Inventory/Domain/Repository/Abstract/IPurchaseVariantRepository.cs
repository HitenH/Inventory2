using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseVariantRepository
    {
        Task Create(PurchaseVariant variant);
        /// <summary>Bulk entry: adds all lines and the new total in one transaction (all or nothing).</summary>
        Task CreateRange(List<PurchaseVariant> variants);
        Task<PurchaseVariant> GetById(Guid id);
        Task<List<PurchaseVariant>> GetAll();
        Task Update(PurchaseVariant variant);
        Task Delete(PurchaseVariant variant);
    }
}
