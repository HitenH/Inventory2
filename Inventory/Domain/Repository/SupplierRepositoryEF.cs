using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class SupplierRepositoryEF : ISupplierRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public SupplierRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(SupplierEntity supplier)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task Delete(SupplierEntity supplier)
        {
            await using var context = await factory.CreateDbContextAsync();
            try
            {
                var hasRelatedPurchases = context.Purchases.Any(p => p.SupplierEntityId == supplier.Id);  
                if (hasRelatedPurchases)
                {
                    throw new Exception("The Supplier cannot be deleted due to association with other entities");
                }
                else
                {
                    var existing = await context.Suppliers.Include(x => x.Mobiles).FirstOrDefaultAsync(x => x.Id == supplier.Id);
                    if (existing != null)
                    {
                        context.Suppliers.Remove(existing);   // its phone numbers are removed with it (cascade)
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<List<SupplierEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Suppliers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(n => n.Mobiles)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<List<SupplierEntity>> GetAllForLookup()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, decimal>> GetPurchaseTotalsBySupplierAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Purchases
                .AsNoTracking()
                .GroupBy(p => p.SupplierEntityId)
                .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.TotalAmountProduct ?? 0) })
                .ToDictionaryAsync(x => x.SupplierId, x => x.Total);
        }

        public async Task<SupplierEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
           return await context.Suppliers.AsNoTracking().Include(n => n.Mobiles).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SupplierEntity supplier)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Suppliers.Include(x => x.Mobiles).FirstOrDefaultAsync(x => x.Id == supplier.Id);
            if (existing is null)
                return;

            existing.SupplierId = supplier.SupplierId;
            existing.Name = supplier.Name;
            existing.ContactPerson = supplier.ContactPerson;
            existing.Address = supplier.Address;
            existing.Area = supplier.Area;
            existing.Remarks = supplier.Remarks;

            // Phone numbers of THIS record only: update kept numbers, add new ones, delete removed ones.
            // (Replaces the old global "delete every empty number in the database" sweep.)
            var wanted = supplier.Mobiles ?? new List<Mobile>();
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
                context.Mobiles.Add(new Mobile { Phone = add.Phone, SupplierEntityId = existing.Id });
            }

            await context.SaveChangesAsync();
        }

        public bool IsSupplierIdExist(string supplierId)
        {
            using var context = factory.CreateDbContext();
            return context.Suppliers.Any(c => c.SupplierId!.ToLower() == supplierId.Trim().ToLower());
        }
        public bool IsSupplierIdExist(string supplierId, Guid id)
        {
            using var context = factory.CreateDbContext();
            var result = context.Suppliers.Any(c => c.SupplierId!.ToLower() == supplierId.Trim().ToLower() && c.Id == id);
            if (result)
                return false;
            else
                return IsSupplierIdExist(supplierId);
        }
    }
}
