using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class CustomerRepositoryEF : ICustomerRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public CustomerRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }
        public async Task Create(CustomerEntity customer)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Customers.AddAsync(customer);
            await context.SaveChangesAsync();
        }

        public async Task Delete(CustomerEntity customer)
        {
            await using var context = await factory.CreateDbContextAsync();
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
                    var existing = await context.Customers.Include(x => x.Mobiles).FirstOrDefaultAsync(x => x.Id == customer.Id);
                    if (existing != null)
                    {
                        context.Customers.Remove(existing);   // its phone numbers are removed with it (cascade)
                        await context.SaveChangesAsync();
                    }
                }  
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<List<CustomerEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Customers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(n => n.Mobiles)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // Batch 14: totals are added up in the database instead of loading every sale (was .Include(c => c.Sales)).
        public async Task<Dictionary<Guid, decimal>> GetSalesTotalsByCustomerAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Sales
                .AsNoTracking()
                .GroupBy(s => s.CustomerEntityId)
                .Select(g => new { CustomerId = g.Key, Total = g.Sum(s => s.TotalAmountProduct) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Total);
        }

        public async Task<List<AreaSalesTotal>> GetSalesTotalsByAreaAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Sales
                .AsNoTracking()
                .GroupBy(s => s.Customer!.Area)
                .Select(g => new AreaSalesTotal(g.Key, g.Sum(s => s.TotalAmountProduct)))
                .ToListAsync();
        }

        public async Task<List<CustomerEntity>> GetAllForLookup()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Customers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<CustomerEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Customers.AsNoTracking().Include(n => n.Mobiles).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(CustomerEntity customer)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Customers.Include(x => x.Mobiles).FirstOrDefaultAsync(x => x.Id == customer.Id);
            if (existing is null)
                return;

            existing.CustomerId = customer.CustomerId;
            existing.Name = customer.Name;
            existing.ContactPerson = customer.ContactPerson;
            existing.Address = customer.Address;
            existing.Area = customer.Area;
            existing.Remarks = customer.Remarks;

            // Phone numbers of THIS record only: update kept numbers, add new ones, delete removed ones.
            // (Replaces the old global "delete every empty number in the database" sweep.)
            var wanted = customer.Mobiles ?? new List<Mobile>();
            var current = existing.Mobiles ?? new List<Mobile>();
            foreach (var old in current.ToList())
            {
                var match = wanted.FirstOrDefault(w => w.Id != 0 && w.Id == old.Id);
                if (match is null)
                    context.Mobiles.Remove(old);
                else
                    old.Phone = match.Phone;
            }
            foreach (var add in wanted.Where(w => w.Id == 0 || current.All(c => c.Id != w.Id)))
            {
                context.Mobiles.Add(new Mobile { Phone = add.Phone, CustomerEntityId = existing.Id });
            }

            await context.SaveChangesAsync();
        }

        public bool IsCustomIdExist(string customerId)
        {
            using var context = factory.CreateDbContext();
            return context.Customers.Any(c => c.CustomerId!.ToLower() == customerId.Trim().ToLower());
        }
        public bool IsCustomIdExist(string customerId, Guid id)
        {
            using var context = factory.CreateDbContext();
            var result = context.Customers.Any(c => c.CustomerId!.ToLower() == customerId.Trim().ToLower() && c.Id == id);
            if (result)
                return false;
            else
                return IsCustomIdExist(customerId);
        }
    }
}
