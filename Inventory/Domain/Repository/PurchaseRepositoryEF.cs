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
            var entity = new PurchaseEntity
            {
                SupplierEntityId = purchase.SupplierEntityId != Guid.Empty
                    ? purchase.SupplierEntityId
                    : purchase.Supplier?.Id ?? Guid.Empty,
                Date = purchase.Date,
                VoucherId = purchase.VoucherId,
                Remarks = purchase.Remarks,
                Discount = purchase.Discount,
                TotalAmountProduct = purchase.TotalAmountProduct
            };

            await context.Purchases.AddAsync(entity);
            await context.SaveChangesAsync();
            return entity.Id;
        }

        public async Task Delete(PurchaseEntity purchase)
        {
            var existing = await context.Purchases
                .Include(p => p.PurchaseVariants)
                .FirstOrDefaultAsync(p => p.Id == purchase.Id);

            if (existing is null)
            {
                return;
            }

            context.Purchases.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<PurchaseEntity>> GetAll()
        {
            return await context.Purchases
                .AsNoTracking()
                .Include(p => p.Supplier)
                .ToListAsync();
        }

        public async Task<PurchaseEntity> GetById(Guid id)
        {
            var purchase = await context.Purchases
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseVariants)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (purchase is null)
            {
                return new PurchaseEntity();
            }

            await TransactionLineHydrator.HydratePurchaseAsync(context, purchase);
            return purchase;
        }

        public async Task<int> GetLastVoucherIdByDate(DateOnly date)
        {
            var purchase = await context.Purchases
                .AsNoTracking()
                .OrderByDescending(p => p.VoucherId)
                .FirstOrDefaultAsync(p => p.Date == date);

            return purchase?.VoucherId ?? 0;
        }

        public async Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date)
        {
            return await context.Purchases
                .AsNoTracking()
                .AnyAsync(p => p.VoucherId == voucherId && p.Date == date);
        }

        public async Task<Guid> Update(PurchaseEntity purchase)
        {
            var existing = await context.Purchases.FirstOrDefaultAsync(p => p.Id == purchase.Id);
            if (existing is null)
            {
                return purchase.Id;
            }

            existing.SupplierEntityId = purchase.SupplierEntityId != Guid.Empty
                ? purchase.SupplierEntityId
                : purchase.Supplier?.Id ?? existing.SupplierEntityId;
            existing.Date = purchase.Date;
            existing.VoucherId = purchase.VoucherId;
            existing.Remarks = purchase.Remarks;
            existing.Discount = purchase.Discount;
            existing.TotalAmountProduct = purchase.TotalAmountProduct;

            await context.SaveChangesAsync();
            return existing.Id;
        }

    }
}
