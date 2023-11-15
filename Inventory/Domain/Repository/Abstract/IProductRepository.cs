using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IProductRepository
    {
        Task<Guid> Create(ProductEntity product);
        Task<ProductEntity> GetById(Guid id);
        Task<ProductEntity> GetByProductId(string productId);
        Task<List<ProductEntity>> GetAll();
        Task Update(ProductEntity product);
        Task Delete(ProductEntity product);
        bool IsProductIdExist(string productId);
        bool IsProductIdExist(string productId, Guid id);
    }
}
