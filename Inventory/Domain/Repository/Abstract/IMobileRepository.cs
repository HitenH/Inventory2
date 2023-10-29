using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IMobileRepository
    {
        Task Create(Mobile mobile);
        Task<Mobile> GetById(int id);
        Task<List<Mobile>> GetAll();
        Task Update(Mobile mobile);
        Task Delete(Mobile mobile);
    }
}
