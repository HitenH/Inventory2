using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ICategoryRepository
    {
        Task Create(CategoryEntity category);
        Task<CategoryEntity> GetById(Guid id);
        Task<List<CategoryEntity>> GetAll();
        Task Update(CategoryEntity category);
        Task Delete(CategoryEntity category);
    }
}
