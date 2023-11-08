using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseRepository
    {
        Task<Guid> Create(PurchaseEntity purchase);
        Task<PurchaseEntity> GetById(Guid id);
        Task<List<PurchaseEntity>> GetAll();
        Task<Guid> Update(PurchaseEntity purchase);
        Task Delete(PurchaseEntity purchase);
        Task<int> GetLastVoucherIdByDate(DateOnly date);
        Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date);
    }
}
