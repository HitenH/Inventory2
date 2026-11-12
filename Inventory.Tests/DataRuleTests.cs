using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>Batch 5: unique IDs (ignoring case), no blank records, lines must point to a real product + its own variant.</summary>
public class DataRuleTests
{
    private static SalesEntity Sale(Seed seed, Guid productId, Guid variantId) => new()
    {
        Date = new DateOnly(2030, 3, 1),
        CustomerEntityId = seed.CustomerId,
        VoucherId = 1,
        SalesVariants = new() { new SalesVariantEntity { ProductEntityId = productId, VariantEntityId = variantId, Quantity = 1 } }
    };

    [Fact]
    public async Task Duplicate_product_id_is_refused_whatever_the_case()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var products = new ProductRepositoryEF(db.Factory);

        Assert.True(products.IsProductIdExist("p1"));
        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => products.Create(new ProductEntity { ProductId = "p1", Name = "Copy" }));
        Assert.True(DbErrors.IsUniqueViolation(ex));
    }

    [Fact]
    public async Task Duplicate_customer_and_supplier_ids_are_refused()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => new CustomerRepositoryEF(db.Factory).Create(new CustomerEntity { CustomerId = "c1", Name = "Copy" }));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => new SupplierRepositoryEF(db.Factory).Create(new SupplierEntity { SupplierId = "s1", Name = "Copy" }));
    }

    [Fact]
    public async Task Same_variant_id_is_allowed_on_another_product_but_not_twice_on_one()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var variants = new VariantRepositoryEF(db.Factory);
        var products = new ProductRepositoryEF(db.Factory);

        var otherId = await products.Create(new ProductEntity { ProductId = "P2", Name = "Product Two" });
        await variants.Create(new VariantEntity { VariantId = "V1", Name = "Red", ProductEntityId = otherId });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => variants.Create(new VariantEntity { VariantId = "v1", Name = "Again", ProductEntityId = seed.ProductId }));
    }

    [Fact]
    public async Task Blank_product_and_customer_are_refused_with_a_readable_message()
    {
        using var db = new TestDatabase();
        await db.SeedAsync();
        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => new ProductRepositoryEF(db.Factory).Create(new ProductEntity { ProductId = "", Name = "No ID" }));
        Assert.False(string.IsNullOrWhiteSpace(DbErrors.RuleMessage(ex)));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => new CustomerRepositoryEF(db.Factory).Create(new CustomerEntity { CustomerId = "C9", Name = " " }));
    }

    [Fact]
    public async Task A_line_with_another_products_variant_is_refused()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var otherId = await new ProductRepositoryEF(db.Factory).Create(new ProductEntity { ProductId = "P2", Name = "Product Two" });

        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => new SaleRepositoryEF(db.Factory).Create(Sale(seed, otherId, seed.Variant1Id)));
        Assert.NotNull(DbErrors.RuleMessage(ex));

        await using var c = db.Open();
        Assert.Equal(0, c.Sales.Count());   // nothing half-saved
    }

    [Fact]
    public async Task Customer_with_sales_cannot_be_deleted()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await new SaleRepositoryEF(db.Factory).Create(Sale(seed, seed.ProductId, seed.Variant1Id));
        var customers = new CustomerRepositoryEF(db.Factory);

        await Assert.ThrowsAnyAsync<Exception>(async () => await customers.Delete(await customers.GetById(seed.CustomerId)));
        await using var c = db.Open();
        Assert.True(c.Customers.Any(x => x.Id == seed.CustomerId));
    }
}
