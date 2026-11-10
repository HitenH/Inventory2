using Inventory.Domain.Entities;
using Inventory.Models;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IProductRepository
    {
        Task<Guid> Create(ProductEntity product);
        Task<ProductEntity> GetById(Guid id);
        Task<ProductEntity> GetProductWithVariantsById(Guid id);
        Task<ProductEntity> GetByProductId(string productId);
        Task<List<ProductEntity>> GetAll();
        Task<List<ProductEntity>> GetAllForLookup();
        Task<List<ProductReportItem>> GetProductStockReportAsync(bool onlyBelowReorderLevel = false);
        Task<Dictionary<Guid, int>> GetStockInHandByVariantIdsAsync(IEnumerable<Guid> variantIds);
        Task Update(ProductEntity product);
        Task Delete(ProductEntity product);
        bool IsProductIdExist(string productId);
        bool IsProductIdExist(string productId, Guid id);
    }
}
