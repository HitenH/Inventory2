using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IVariantRepository
    {
        Task Create(VariantEntity variant);
        Task<VariantEntity> GetById(Guid id);
        Task<List<VariantEntity>> GetAll();
        Task Update(VariantEntity variant);
        Task Delete(VariantEntity variant);
    }
}
