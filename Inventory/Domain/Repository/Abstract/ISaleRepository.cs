using Inventory.Domain.Entities;
using System.Threading.Tasks;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ISaleRepository
    {
        Task Create(SalesEntity sale);
        Task<SalesEntity> GetById(Guid id);
        Task<List<SalesEntity>> GetAll();
        Task Update(SalesEntity sale);
        Task Delete(SalesEntity sale);
        Task DeleteRange(List<SalesEntity> sales);

    }
}
