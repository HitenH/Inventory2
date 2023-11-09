using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISalesSummaryRepository
    {
        Task<Guid> Create(SalesSummaryEntity model);
        Task<SalesSummaryEntity> GetById(Guid id);
        Task<List<SalesSummaryEntity>> GetAll();
        Task Update(SalesSummaryEntity model);
        Task Delete(SalesSummaryEntity model);
        Task DeleteRange(List<SalesSummaryEntity> models);
    }
}
