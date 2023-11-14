using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class CustomerRepositoryEF : ICustomerRepository
    {
        private readonly AppDbContext context;

        public CustomerRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(CustomerEntity customer)
        {
            await context.Customers.AddAsync(customer);
            await context.SaveChangesAsync();
        }

        public async Task Delete(CustomerEntity customer)
        {
            try
            {
                var hasRelatedSalesOrder = context.SalesOrders.Any(p => p.CustomerEntityId == customer.Id);
                var hasRelatedSales = context.Sales.Any(p => p.CustomerEntityId == customer.Id);

                if (hasRelatedSalesOrder || hasRelatedSales)
                {
                    throw new Exception("The Customer cannot be deleted due to association with other entities");
                }
                else
                {
                    context.Customers.Remove(customer);
                    await context.SaveChangesAsync();
                }  
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<List<CustomerEntity>> GetAll()
        {
            return await context.Customers.Include(n => n.Mobiles).Include(c => c.Sales).AsNoTracking().ToListAsync();
        }

        public async Task<CustomerEntity> GetById(Guid id)
        {
            return await context.Customers.Include(n => n.Mobiles).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(CustomerEntity customer)
        {
            context.Customers.Update(customer);
            await context.SaveChangesAsync();
        }

        public bool IsCustomIdExist(string customerId)
        {
            return context.Customers.Any(c => c.CustomerId == customerId);
        }
        public bool IsCustomIdExist(string customerId, Guid id)
        {
            var result = context.Customers.Any(c => c.CustomerId == customerId && c.Id == id);
            if (result)
                return false;
            else
                return IsCustomIdExist(customerId);
        }
    }
}
