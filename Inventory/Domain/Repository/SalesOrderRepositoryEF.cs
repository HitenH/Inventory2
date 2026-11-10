using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository;

public class SalesOrderRepositoryEF : ISalesOrderRepository
{
    private readonly AppDbContext context;

    public SalesOrderRepositoryEF(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<Guid> Create(SalesOrderEntity order)
    {
        var entity = new SalesOrderEntity
        {
            CustomerEntityId = order.CustomerEntityId != Guid.Empty
                ? order.CustomerEntityId
                : order.Customer?.Id ?? Guid.Empty,
            Date = order.Date,
            DueDate = order.DueDate,
            VoucherId = order.VoucherId,
            OrderStatus = order.OrderStatus,
            TotalAmountProduct = order.TotalAmountProduct
        };

        await context.SalesOrders.AddAsync(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task Delete(SalesOrderEntity order)
    {
        var existing = await context.SalesOrders
            .Include(o => o.SalesOrderVariants)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        if (existing is null)
        {
            return;
        }

        context.SalesOrders.Remove(existing);
        await context.SaveChangesAsync();
    }

    public async Task DeleteRange(List<SalesOrderEntity> orders)
    {
        context.SalesOrders.RemoveRange(orders);
        await context.SaveChangesAsync();
    }

    public async Task<List<SalesOrderEntity>> GetAll()
    {
        return await context.SalesOrders
            .AsNoTracking()
            .Include(p => p.Customer)
            .ToListAsync();
    }

    public async Task<SalesOrderEntity> GetById(Guid id)
    {
        var order = await context.SalesOrders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Customer)
            .Include(p => p.SalesOrderVariants)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (order is null)
        {
            return new SalesOrderEntity();
        }

        await TransactionLineHydrator.HydrateSalesOrderAsync(context, order);
        return order;
    }

    public async Task<int> GetLastVoucherIdByDate(DateOnly date)
    {
        var order = await context.SalesOrders
            .AsNoTracking()
            .OrderByDescending(p => p.VoucherId)
            .FirstOrDefaultAsync(s => s.Date == date);

        return order?.VoucherId ?? 0;
    }

    public async Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date)
    {
        return await context.SalesOrders
            .AsNoTracking()
            .AnyAsync(p => p.VoucherId == voucherId && p.Date == date);
    }

    public async Task<Guid> Update(SalesOrderEntity order)
    {
        var existing = await context.SalesOrders.FirstOrDefaultAsync(o => o.Id == order.Id);
        if (existing is null)
        {
            return order.Id;
        }

        existing.CustomerEntityId = order.CustomerEntityId != Guid.Empty
            ? order.CustomerEntityId
            : order.Customer?.Id ?? existing.CustomerEntityId;
        existing.Date = order.Date;
        existing.DueDate = order.DueDate;
        existing.VoucherId = order.VoucherId;
        existing.OrderStatus = order.OrderStatus;
        existing.TotalAmountProduct = order.TotalAmountProduct;

        await context.SaveChangesAsync();
        return existing.Id;
    }
}
