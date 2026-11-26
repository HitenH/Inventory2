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
        /// <param name="places">Stage 3: count only the sellable pieces in these places (null = every place).</param>
        Task<List<ProductReportItem>> GetProductStockReportAsync(bool onlyBelowReorderLevel = false, IReadOnlySet<int>? places = null);
        Task<Dictionary<Guid, int>> GetStockInHandByVariantIdsAsync(IEnumerable<Guid> variantIds);
        /// <param name="places">Stage 3: count only the sellable pieces in these places (null = every place).</param>
        Task<Dictionary<Guid, StockFigures>> GetStockByVariantIdsAsync(IEnumerable<Guid> variantIds, IReadOnlySet<int>? places = null);
        /// <summary>Stage 3, batch 3-3: sellable pieces per place for each variant, by place number, only places in <paramref name="places"/> (null = every place) that hold pieces.</summary>
        Task<Dictionary<Guid, List<PlaceStock>>> GetPlaceStockAsync(IEnumerable<Guid> variantIds, IReadOnlySet<int>? places = null);
        Task Update(ProductEntity product);
        Task Delete(ProductEntity product);
        bool IsProductIdExist(string productId);
        bool IsProductIdExist(string productId, Guid id);
    }
}
