using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository;

// Batch 6: each method uses its own short-lived context from the factory.
public class SalesOrderRepositoryEF : ISalesOrderRepository
{
    private readonly IDbContextFactory<AppDbContext> factory;

    public SalesOrderRepositoryEF(IDbContextFactory<AppDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task<Guid> Create(SalesOrderEntity order)
    {
        await using var context = await factory.CreateDbContextAsync();
        var entity = new SalesOrderEntity
        {
            CustomerEntityId = order.CustomerEntityId != Guid.Empty
                ? order.CustomerEntityId
                : order.Customer?.Id ?? Guid.Empty,
            Date = order.Date,
            DueDate = order.DueDate,
            VoucherId = order.VoucherId,
            OrderStatus = order.OrderStatus,
            TotalAmountProduct = order.TotalAmountProduct,
            DiscountAmount = order.DiscountAmount
        };

        await context.SalesOrders.AddAsync(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task Delete(SalesOrderEntity order)
    {
        await using var context = await factory.CreateDbContextAsync();
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
        await using var context = await factory.CreateDbContextAsync();
        // Load by ID in this context (the list came from another, already closed context).
        var ids = orders.Select(o => o.Id).ToList();
        var existing = await context.SalesOrders
            .Include(o => o.SalesOrderVariants)
            .Where(o => ids.Contains(o.Id))
            .ToListAsync();
        context.SalesOrders.RemoveRange(existing);
        await context.SaveChangesAsync();
    }

    public async Task<List<SalesOrderEntity>> GetAll()
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.SalesOrders
            .AsNoTracking()
            .Include(p => p.Customer)
            .ToListAsync();
    }

    public async Task<SalesOrderEntity> GetById(Guid id)
    {
        await using var context = await factory.CreateDbContextAsync();
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
        await using var context = await factory.CreateDbContextAsync();
        var order = await context.SalesOrders
            .AsNoTracking()
            .OrderByDescending(p => p.VoucherId)
            .FirstOrDefaultAsync(s => s.Date == date);

        return order?.VoucherId ?? 0;
    }

    public async Task<bool> IsVoucherExistByDate(int voucherId, DateOnly date)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.SalesOrders
            .AsNoTracking()
            .AnyAsync(p => p.VoucherId == voucherId && p.Date == date);
    }

    public async Task<Guid> Update(SalesOrderEntity order)
    {
        await using var context = await factory.CreateDbContextAsync();
        // Write-locks the database before reading, so a line saved at the same moment cannot give a wrong total.
        await using var transaction = await context.Database.BeginTransactionAsync();
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
        existing.DiscountAmount = order.DiscountAmount;
        // Batch 7: the total is always recalculated from the saved lines and this discount.
        await TransactionTotals.RecalculateSalesOrderAsync(context, existing.Id);

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return existing.Id;
    }

    /// <summary>Saves only the voucher discount (as soon as it is typed) and the recalculated total.</summary>
    public async Task UpdateDiscount(Guid salesOrderId, decimal discount)
    {
        await using var context = await factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var existing = await context.SalesOrders.FirstOrDefaultAsync(o => o.Id == salesOrderId);
        if (existing is null)
        {
            return;
        }

        existing.DiscountAmount = discount;
        await TransactionTotals.RecalculateSalesOrderAsync(context, existing.Id);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
