using Inventory.Domain.Entities;
using System.Threading.Tasks;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISaleRepository
    {
        Task<Guid> Create(SalesEntity sale);
        Task<SalesEntity> GetById(Guid id);
        Task<List<SalesEntity>> GetAll();
        /// <summary>Sum of sale totals dated from..to (both included), added up in the database (batch 14).</summary>
        Task<decimal> GetTotalBetweenAsync(DateOnly from, DateOnly to);
        Task Update(SalesEntity sale);
        Task Delete(SalesEntity sale);
        Task DeleteRange(List<SalesEntity> sales);
        int GetLastVoucherIdByDate(DateOnly date);
        bool IsVoucherExistByDate(int voucherId, DateOnly date);
    }
}
