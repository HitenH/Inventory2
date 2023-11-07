using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISalesOrderRepository
    {
        Task<Guid> Create(SalesOrderEntity order);
        Task<SalesOrderEntity> GetById(Guid id);
        Task<List<SalesOrderEntity>> GetAll();
        Task<Guid> Update(SalesOrderEntity order);
        Task Delete(SalesOrderEntity order);
        Task DeleteRange(List<SalesOrderEntity> orders);
        Task<int> GetLastVoucherId();
        bool IsVoucherExist(int voucherId);
    }
}
