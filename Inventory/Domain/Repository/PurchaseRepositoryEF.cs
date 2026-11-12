using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class PurchaseRepositoryEF : IPurchaseRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public PurchaseRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task<Guid> Create(PurchaseEntity purchase)
        {
            await using var context = await factory.CreateDbContextAsync();
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
            await using var context = await factory.CreateDbContextAsync();
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
            await using var context = await factory.CreateDbContextAsync();
            return await context.Purchases
                .AsNoTracking()
                .Include(p => p.Supplier)
                .ToListAsync();
        }

        public async Task<PurchaseEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
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
            await using var context = await factory.CreateDbContextAsync();
            var purchase = await context.Purchases
                .AsNoTracking()
                .OrderByDescending(p => p.VoucherId)
                .FirstOrDefaultAsync(p => p.Date == date);

            return purchase?.VoucherId ?? 0;
        }

        public async Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Purchases
                .AsNoTracking()
                .AnyAsync(p => p.VoucherId == voucherId && p.Date == date);
        }

        public async Task<Guid> Update(PurchaseEntity purchase)
        {
            await using var context = await factory.CreateDbContextAsync();
            // Write-locks the database before reading, so a line saved at the same moment cannot give a wrong total.
            await using var transaction = await context.Database.BeginTransactionAsync();
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
            // Batch 7: the total is always recalculated from the saved lines and this discount.
            await TransactionTotals.RecalculatePurchaseAsync(context, existing.Id);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return existing.Id;
        }

        /// <summary>Saves only the voucher discount (as soon as it is typed) and the recalculated total.</summary>
        public async Task UpdateDiscount(Guid purchaseId, decimal discount)
        {
            await using var context = await factory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var existing = await context.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId);
            if (existing is null)
            {
                return;
            }

            existing.Discount = discount;
            await TransactionTotals.RecalculatePurchaseAsync(context, existing.Id);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

    }
}
