using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
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
            context.Customers.Remove(customer);
            await context.SaveChangesAsync();
        }

        public async Task<List<CustomerEntity>> GetAll()
        {
            return await context.Customers.Include(n=>n.Mobiles).AsNoTracking().ToListAsync();
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
    }
}
