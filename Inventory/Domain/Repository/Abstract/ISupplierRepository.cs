using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISupplierRepository
    {
        Task Create(SupplierEntity supplier);
        Task<SupplierEntity> GetById(Guid id);
        Task<List<SupplierEntity>> GetAll();
        Task Update(SupplierEntity supplier);
        Task Delete(SupplierEntity supplier);
    }
}
