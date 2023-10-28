using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IProductRepository
    {
        Task Create(ProductEntity product);
        Task<ProductEntity> GetById(Guid id);
        Task<List<ProductEntity>> GetAll();
        Task Update(ProductEntity product);
        Task Delete(ProductEntity product);
    }
}
