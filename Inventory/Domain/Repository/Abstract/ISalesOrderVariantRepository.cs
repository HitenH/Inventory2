using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISalesOrderVariantRepository
    {
        Task Create(SalesOrderVariantEntity variant);
        /// <summary>Bulk entry: adds all lines and the new total in one transaction (all or nothing).</summary>
        Task CreateRange(List<SalesOrderVariantEntity> variants);
        Task<SalesOrderVariantEntity> GetById(Guid id);
        Task<List<SalesOrderVariantEntity>> GetAll();
        Task Update(SalesOrderVariantEntity variant);
        Task Delete(SalesOrderVariantEntity variant);
    }
}
