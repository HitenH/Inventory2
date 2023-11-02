using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
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
        public async Task Create(PurchaseEntity purchase)
        {
            //try
            //{
            //    await context.Purchases.AddAsync(purchase);
            //    await context.SaveChangesAsync();
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //}
           
        }

        public async Task Delete(PurchaseEntity purchase)
        {
            //context.Purchases.Remove(purchase);
            //await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseEntity>> GetAll()
        {
            return new();
            //return await context.Purchases.AsNoTracking().Include(p => p.Supplier).ToListAsync();
        }

        public async Task<PurchaseEntity> GetById(Guid id)
        {
            return new();
            //return await context.Purchases.Include(p => p.Supplier).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(PurchaseEntity purchase)
        {
            //context.Purchases.Update(purchase);
            //await context.SaveChangesAsync();
        }

    }
}
