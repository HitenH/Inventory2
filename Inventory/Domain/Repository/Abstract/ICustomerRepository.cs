using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface ICustomerRepository
    {
        Task Create(CustomerEntity customer);
        Task<CustomerEntity> GetById(Guid id);
        Task<List<CustomerEntity>> GetAll();
        Task Update(CustomerEntity customer);
        Task Delete(CustomerEntity customer);

    }
}
