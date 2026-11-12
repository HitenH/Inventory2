using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// A brand-new SQLite database file for one test, created with the app's real migrations
/// (so the unique rules and triggers from batch 5 are included). Deleted afterwards.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public string Folder { get; }
    public string DatabasePath { get; }
    public IDbContextFactory<AppDbContext> Factory { get; }

    public TestDatabase()
    {
        Folder = Path.Combine(Path.GetTempPath(), "InventoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        DatabasePath = Path.Combine(Folder, "Inventory.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;
        Factory = new ContextFactory(options);
        using var context = Factory.CreateDbContext();
        context.Database.Migrate();
    }

    public AppDbContext Open() => Factory.CreateDbContext();

    /// <summary>One supplier, one customer, one product "P1" with variants "V1" and "V2".</summary>
    public async Task<Seed> SeedAsync()
    {
        await using var context = Open();
        var supplier = new SupplierEntity { SupplierId = "S1", Name = "Supplier One" };
        var customer = new CustomerEntity { CustomerId = "C1", Name = "Customer One" };
        var product = new ProductEntity { ProductId = "P1", Name = "Product One", Rate = 50m };
        var v1 = new VariantEntity { VariantId = "V1", Name = "Red", Product = product, ReorderLevel = 5 };
        var v2 = new VariantEntity { VariantId = "V2", Name = "Blue", Product = product, ReorderLevel = 5 };
        context.AddRange(supplier, customer, product, v1, v2);
        await context.SaveChangesAsync();
        return new Seed(supplier.Id, customer.Id, product.Id, v1.Id, v2.Id);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Folder, recursive: true); } catch { /* temp folder; ignore */ }
    }

    private sealed class ContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}

public sealed record Seed(Guid SupplierId, Guid CustomerId, Guid ProductId, Guid Variant1Id, Guid Variant2Id);
