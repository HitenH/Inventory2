using System.Data.Common;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventory.Tests;

/// <summary>
/// Batch 14 (P4): saving one variant on the Product page writes only that variant – not the product,
/// the other variants or their photos. Counts the database write commands actually sent.
/// </summary>
public class VariantSaveWriteTests
{
    private sealed class WriteCounter : DbCommandInterceptor
    {
        public List<string> Writes { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Record(DbCommand command)
        {
            foreach (var statement in command.CommandText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (statement.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                    || statement.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                    || statement.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    Writes.Add(statement.Split('(', ' ', '\n')[0..3] is var words ? string.Join(' ', words).Trim() : statement);
                }
            }
        }
    }

    private sealed class CountingFactory(string databasePath, WriteCounter counter) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .AddInterceptors(counter)
            .Options);
    }

    [Fact]
    public async Task Editing_one_variant_writes_only_that_variant()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var c = db.Open())
        {
            c.Images.Add(new Image { Id = Guid.NewGuid(), ImageTitle = "blue.jpg", ImageData = new byte[] { 0xFF, 0xD8, 1, 2, 3 }, VariantEntityId = seed.Variant2Id });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var counter = new WriteCounter();
        var products = new ProductRepositoryEF(new CountingFactory(db.DatabasePath, counter));

        // What the Product page does: load the product, change one variant, save the product.
        var product = await products.GetById(seed.ProductId);
        product.Variants!.Single(v => v.Id == seed.Variant1Id).ReorderLevel = 9;
        counter.Writes.Clear();
        await products.Update(product);

        var write = Assert.Single(counter.Writes);
        Assert.Contains("Variants", write);

        await using var check = db.Open();
        Assert.Equal(9, check.Variants.Single(v => v.Id == seed.Variant1Id).ReorderLevel);
        Assert.NotNull(check.Images.SingleOrDefault(i => i.VariantEntityId == seed.Variant2Id));   // other variant's photo untouched
    }

    [Fact]
    public async Task Adding_a_variant_writes_only_the_new_variant()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var counter = new WriteCounter();
        var products = new ProductRepositoryEF(new CountingFactory(db.DatabasePath, counter));

        var product = await products.GetById(seed.ProductId);
        product.Variants!.Add(new VariantEntity { VariantId = "V3", Name = "Green", ReorderLevel = 1 });
        counter.Writes.Clear();
        await products.Update(product);

        var write = Assert.Single(counter.Writes);
        Assert.StartsWith("INSERT", write);
        Assert.Contains("Variants", write);
    }

    [Fact]
    public async Task Saving_with_nothing_changed_writes_nothing()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var counter = new WriteCounter();
        var products = new ProductRepositoryEF(new CountingFactory(db.DatabasePath, counter));

        var product = await products.GetById(seed.ProductId);
        counter.Writes.Clear();
        await products.Update(product);

        Assert.Empty(counter.Writes);
    }
}
