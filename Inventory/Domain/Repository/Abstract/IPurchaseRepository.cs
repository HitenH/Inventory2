using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IPurchaseRepository
    {
        Task<Guid> Create(PurchaseEntity purchase);
        Task<PurchaseEntity> GetById(Guid id);
        Task<List<PurchaseEntity>> GetAll();
        /// <summary>Sum of purchase totals dated from..to (both included), added up in the database (batch 14).</summary>
        Task<decimal> GetTotalBetweenAsync(DateOnly from, DateOnly to);
        Task<Guid> Update(PurchaseEntity purchase);
        Task UpdateDiscount(Guid purchaseId, decimal discount);
        Task Delete(PurchaseEntity purchase);
        Task<int> GetLastVoucherIdByDate(DateOnly date);
        Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date);
    }
}
