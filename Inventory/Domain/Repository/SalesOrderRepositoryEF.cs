using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class SalesOrderRepositoryEF: ISalesOrderRepository
    {
        private readonly AppDbContext context;

        public SalesOrderRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<Guid> Create(SalesOrderEntity order)
        {
            try
            {
                await context.SalesOrders.AddAsync(order);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return order.Id;
        }

        public async Task Delete(SalesOrderEntity order)
        {
            context.SalesOrders.Remove(order);
            await context.SaveChangesAsync();
        }

        public async Task<List<SalesOrderEntity>> GetAll()
        {
            return await context.SalesOrders.AsNoTracking().Include(p => p.Customer).ToListAsync();
        }

        public async Task<SalesOrderEntity> GetById(Guid id)
        {
            return await context.SalesOrders.Include(p => p.Customer).Include(p => p.SalesOrderVariants).ThenInclude(p => p.ProductVariant).ThenInclude(p => p.Product).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task<int> GetLastVoucherId()
        {
            var order = await context.SalesOrders.OrderByDescending(p => p.VoucherId).FirstOrDefaultAsync();
            if (order != null)
            {
                return order.VoucherId;
            }
            return 0;
        }

        public bool IsVoucherExist(int voucherId)
        {
            return context.SalesOrders.Any(p => p.VoucherId == voucherId);
        }

        public async Task<Guid> Update(SalesOrderEntity order)
        {
            try
            {
                context.SalesOrders.Update(order);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return order.Id;
        }
    }
}
