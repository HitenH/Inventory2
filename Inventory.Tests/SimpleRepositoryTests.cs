using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Models;

namespace Inventory.Tests;

/// <summary>Batch 12: categories, photos and purchase orders share one plain add / read / change / delete.</summary>
public class SimpleRepositoryTests
{
    [Fact]
    public async Task Purchase_orders_add_read_change_and_delete()
    {
        using var db = new TestDatabase();
        var orders = new PurchaseOrderRepositoryEF(db.Factory);
        var a = new PurchaseOrderEntity { Id = Guid.NewGuid(), ProductId = "P1", Quantity = 2, OrderStatus = OrderStatus.Open };
        var b = new PurchaseOrderEntity { Id = Guid.NewGuid(), ProductId = "P2", Quantity = 5 };
        var c = new PurchaseOrderEntity { Id = Guid.NewGuid(), ProductId = "P3", Quantity = 1 };
        await orders.Create(a); await orders.Create(b); await orders.Create(c);
        Assert.Equal(3, (await orders.GetAll()).Count);

        var loaded = await orders.GetById(a.Id);
        loaded.Quantity = 7;
        await orders.Update(loaded);
        Assert.Equal(7, (await orders.GetById(a.Id)).Quantity);
        Assert.Null(await orders.GetById(Guid.NewGuid()));

        await orders.DeleteRange(new() { a, b });
        Assert.Equal("P3", Assert.Single(await orders.GetAll()).ProductId);
        await orders.Delete(c);
        await orders.Delete(c); // already gone: nothing happens
        Assert.Empty(await orders.GetAll());
    }

    [Fact]
    public async Task Categories_add_change_and_delete()
    {
        using var db = new TestDatabase();
        var categories = new CategoryRepositoryEF(db.Factory);
        var bags = new CategoryEntity { Id = Guid.NewGuid(), CategoryId = "C1", Name = "Bags" };
        await categories.Create(bags);
        bags.Name = "Hand bags";
        await categories.Update(bags);
        Assert.Equal("Hand bags", (await categories.GetById(bags.Id)).Name);
        await categories.Delete(bags);
        Assert.Empty(await categories.GetAll());
    }
}
