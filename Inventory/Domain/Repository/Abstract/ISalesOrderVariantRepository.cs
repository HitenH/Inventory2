using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISalesOrderVariantRepository
    {
        Task Create(SalesOrderVariantEntity variant);
        Task<SalesOrderVariantEntity> GetById(Guid id);
        Task<List<SalesOrderVariantEntity>> GetAll();
        Task Update(SalesOrderVariantEntity variant);
        Task Delete(SalesOrderVariantEntity variant);
    }
}
