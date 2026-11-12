using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Models;

namespace Inventory.Tests;

/// <summary>Batch 14: totals added up in the database give the same numbers the screens showed before.</summary>
public class PerformanceQueryTests
{
    private static readonly DateOnly Day = new(2030, 3, 1);

    [Fact]
    public async Task Customer_and_area_sales_totals_are_added_up_in_the_database()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        Guid other, quiet;
        await using (var c = db.Open())
        {
            var north = c.Customers.Single(x => x.Id == seed.CustomerId);
            north.Area = "North";
            var second = new CustomerEntity { CustomerId = "C2", Name = "Customer Two", Area = "North" };
            var third = new CustomerEntity { CustomerId = "C3", Name = "Customer Three", Area = "South" };
            c.AddRange(second, third);
            c.Sales.AddRange(
                new SalesEntity { Date = Day, VoucherId = 1, CustomerEntityId = seed.CustomerId, TotalAmountProduct = 100.25m },
                new SalesEntity { Date = Day, VoucherId = 2, CustomerEntityId = seed.CustomerId, TotalAmountProduct = 50m },
                new SalesEntity { Date = Day, VoucherId = 3, CustomerEntityId = second.Id, TotalAmountProduct = 10m });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
            other = second.Id;
            quiet = third.Id;
        }

        var customers = new CustomerRepositoryEF(db.Factory);

        var totals = await customers.GetSalesTotalsByCustomerAsync();
        Assert.Equal(150.25m, totals[seed.CustomerId]);
        Assert.Equal(10m, totals[other]);
        Assert.False(totals.ContainsKey(quiet));   // no sales: not in the list…

        // …and the Customers page shows 0 for them, exactly as before.
        var models = (await customers.GetAll()).Select(c => c.ToModel(totals)).ToDictionary(m => m.Id);
        Assert.Equal(150.25m, models[seed.CustomerId].TotalSales);
        Assert.Equal(0m, models[quiet].TotalSales);

        var areas = (await customers.GetSalesTotalsByAreaAsync()).ToDictionary(a => a.Area ?? "", a => a.Total);
        Assert.Equal(160.25m, areas["North"]);
        Assert.False(areas.ContainsKey("South"));
    }
}
