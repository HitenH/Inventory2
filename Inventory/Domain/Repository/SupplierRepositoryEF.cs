using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class SupplierRepositoryEF : ISupplierRepository
    {
        private readonly AppDbContext context;

        public SupplierRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }

        public async Task Create(SupplierEntity supplier)
        {
            await context.Suppliers.AddAsync(supplier);
            await context.SaveChangesAsync();
        }

        public async Task Delete(SupplierEntity supplier)
        {
            try
            {
                var hasRelatedPurchases = context.Purchases.Any(p => p.SupplierEntityId == supplier.Id);  
                if (hasRelatedPurchases)
                {
                    throw new Exception("The Supplier cannot be deleted due to association with other entities");
                }
                else
                {
                    context.Suppliers.Remove(supplier);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<List<SupplierEntity>> GetAll()
        {
            return await context.Suppliers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(n => n.Mobiles)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<List<SupplierEntity>> GetAllForLookup()
        {
            return await context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, decimal>> GetPurchaseTotalsBySupplierAsync()
        {
            return await context.Purchases
                .AsNoTracking()
                .GroupBy(p => p.SupplierEntityId)
                .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.TotalAmountProduct ?? 0) })
                .ToDictionaryAsync(x => x.SupplierId, x => x.Total);
        }

        public async Task<SupplierEntity> GetById(Guid id)
        {
           return await context.Suppliers.Include(n => n.Mobiles).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SupplierEntity supplier)
        {
            context.Suppliers.Update(supplier);
            await context.SaveChangesAsync();
        }

        public bool IsSupplierIdExist(string supplierId)
        {
            return context.Suppliers.Any(c => c.SupplierId == supplierId);
        }
        public bool IsSupplierIdExist(string supplierId, Guid id)
        {
            var result = context.Suppliers.Any(c => c.SupplierId == supplierId && c.Id == id);
            if (result)
                return false;
            else
                return IsSupplierIdExist(supplierId);
        }
    }
}
