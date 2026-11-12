using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public sealed record AreaSalesTotal(string? Area, decimal Total);

    public interface ICustomerRepository
    {
        Task Create(CustomerEntity customer);
        Task<CustomerEntity> GetById(Guid id);
        Task<List<CustomerEntity>> GetAll();
        Task<List<CustomerEntity>> GetAllForLookup();
        /// <summary>Total of all sales per customer, added up in the database (batch 14).</summary>
        Task<Dictionary<Guid, decimal>> GetSalesTotalsByCustomerAsync();
        /// <summary>Total of all sales per customer area, added up in the database (batch 14).</summary>
        Task<List<AreaSalesTotal>> GetSalesTotalsByAreaAsync();
        Task Update(CustomerEntity customer);
        Task Delete(CustomerEntity customer);
        bool IsCustomIdExist(string customerId);
        bool IsCustomIdExist(string customerId, Guid id);
    }
}
