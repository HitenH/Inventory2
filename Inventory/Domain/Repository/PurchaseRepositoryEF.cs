using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class PurchaseRepositoryEF : IPurchaseRepository
    {
        private readonly AppDbContext context;

        public PurchaseRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task<Guid> Create(PurchaseEntity purchase)
        {
            await context.Purchases.AddAsync(purchase);
            await context.SaveChangesAsync();
            return purchase.Id;
        }

        public async Task Delete(PurchaseEntity purchase)
        {
            context.Purchases.Remove(purchase);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseEntity>> GetAll()
        {
           return await context.Purchases.AsNoTracking().Include(p => p.Supplier).ToListAsync();
        }

        public async Task<PurchaseEntity> GetById(Guid id)
        {
          return await context.Purchases.Include(p => p.Supplier).Include(p => p.PurchaseVariants).ThenInclude(p => p.ProductVariant).ThenInclude(p => p.Product).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task<int> GetLastVoucherIdByDate(DateOnly date)
        {
            var purchase = await context.Purchases.OrderByDescending(p => p.VoucherId).FirstOrDefaultAsync(p => p.Date == date);
            if (purchase != null)
            {
                return purchase.VoucherId;
            }
            return 0;
        }

        public async Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date)
        {
            return context.Purchases.Any(p => p.VoucherId == voucherId && p.Date == date);
        }

        public async Task<Guid> Update(PurchaseEntity purchase)
        {
            try
            {
                context.Purchases.Update(purchase);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return purchase.Id;
        }

    }
}
