using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Inventory.Tests;

/// <summary>Version 3.0, batch 1c-1: "created by / changed by / when" is written at every save, in one place.</summary>
public class RecordStampsTests
{
    private static readonly DateOnly Day = new(2026, 11, 25);

    private static async Task<CustomerEntity> CustomerAsync(TestDatabase test, Guid id)
    {
        await using var db = test.Open();
        return await db.Customers.AsNoTracking().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
    }

    private static async Task<PurchaseEntity> PurchaseAsync(TestDatabase test, Guid id)
    {
        await using var db = test.Open();
        return await db.Purchases.AsNoTracking().SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_new_record_gets_who_and_when_and_is_not_marked_changed()
    {
        using var test = new TestDatabase();
        test.CurrentUser.Name = "Hiten";
        var before = DateTime.Now.AddSeconds(-1);

        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await new CustomerRepositoryEF(test.Factory).Create(customer);

        var saved = await CustomerAsync(test, customer.Id);
        Assert.Equal("Hiten", saved.CreatedBy);
        Assert.InRange(saved.CreatedAt!.Value, before, DateTime.Now.AddSeconds(1));
        Assert.Null(saved.ChangedAt);
        Assert.Null(saved.ChangedBy);
    }

    [Fact]
    public async Task An_edit_records_the_changer_and_keeps_the_creator()
    {
        using var test = new TestDatabase();
        var repo = new CustomerRepositoryEF(test.Factory);
        test.CurrentUser.Name = "Hiten";
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await repo.Create(customer);
        var created = await CustomerAsync(test, customer.Id);

        test.CurrentUser.Name = "Admin";
        // The screen sends a record without the stamps; they must not be wiped.
        await repo.Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "One Ltd" });

        var saved = await CustomerAsync(test, customer.Id);
        Assert.Equal("One Ltd", saved.Name);
        Assert.Equal("Hiten", saved.CreatedBy);
        Assert.Equal(created.CreatedAt, saved.CreatedAt);
        Assert.Equal("Admin", saved.ChangedBy);
        Assert.NotNull(saved.ChangedAt);
    }

    [Fact]
    public async Task Saving_without_a_real_change_does_not_mark_the_record_changed()
    {
        using var test = new TestDatabase();
        var repo = new CustomerRepositoryEF(test.Factory);
        test.CurrentUser.Name = "Hiten";
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await repo.Create(customer);

        test.CurrentUser.Name = "Admin";
        await repo.Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "One" });

        Assert.Null((await CustomerAsync(test, customer.Id)).ChangedBy);
    }

    [Fact]
    public async Task A_new_phone_number_counts_as_a_change_to_the_customer()
    {
        using var test = new TestDatabase();
        var repo = new CustomerRepositoryEF(test.Factory);
        test.CurrentUser.Name = "Hiten";
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await repo.Create(customer);

        test.CurrentUser.Name = "Admin";
        await repo.Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "One", Mobiles = [new Mobile { Phone = "9876543210" }] });

        Assert.Equal("Admin", (await CustomerAsync(test, customer.Id)).ChangedBy);
    }

    [Fact]
    public async Task Adding_changing_and_deleting_a_line_count_as_changes_to_the_purchase()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        test.CurrentUser.Name = "Hiten";
        var purchaseId = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        var lines = new PurchaseVariantRepositoryEF(test.Factory);
        Assert.Equal("Hiten", (await PurchaseAsync(test, purchaseId)).CreatedBy);

        // A line with rate 0 leaves the total unchanged, so only the line rule can mark the purchase.
        test.CurrentUser.Name = "Admin";
        await lines.Create(new PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 5 });
        Assert.Equal("Admin", (await PurchaseAsync(test, purchaseId)).ChangedBy);

        await using (var db = test.Open())
        {
            var line = await db.PurchaseVariant.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            test.CurrentUser.Name = "Clerk";
            line.Quantity = 3;
            await lines.Update(line);
            Assert.Equal("Clerk", (await PurchaseAsync(test, purchaseId)).ChangedBy);

            test.CurrentUser.Name = "Hiten";
            await lines.Delete(line);
        }

        var purchase = await PurchaseAsync(test, purchaseId);
        Assert.Equal("Hiten", purchase.ChangedBy);
        Assert.Equal("Hiten", purchase.CreatedBy);
    }

    [Fact]
    public async Task A_new_variant_counts_as_a_change_to_the_product()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        test.CurrentUser.Name = "Admin";

        await using (var db = test.Open())
        {
            db.Variants.Add(new VariantEntity { VariantId = "V3", Name = "Green", ProductEntityId = seed.ProductId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = test.Open();
        Assert.Equal("Admin", (await check.Products.SingleAsync(TestContext.Current.CancellationToken)).ChangedBy);
    }

    [Fact]
    public async Task Role_switches_and_a_users_roles_count_as_changes_to_the_role_and_the_user()
    {
        using var test = new TestDatabase();
        int roleId, userId;
        await using (var db = test.Open())
        {
            var role = new RoleEntity { Name = "Sales" };
            var user = new UserEntity { Name = "Clerk", Password = "x" };
            db.AddRange(role, user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            (roleId, userId) = (role.Id, user.Id);
        }

        test.CurrentUser.Name = "Hiten";
        Assert.True((await new RoleAdmin(test.Factory).UpdateAsync(roleId, "Sales", [Permissions.ScreenSales])).Ok);
        await using (var db = test.Open())
        {
            db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = roleId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = test.Open();
        Assert.Equal("Hiten", (await check.Roles.SingleAsync(r => r.Id == roleId, TestContext.Current.CancellationToken)).ChangedBy);
        Assert.Equal("Hiten", (await check.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken)).ChangedBy);
    }

    [Fact]
    public async Task A_save_by_the_app_itself_has_a_time_but_no_person()
    {
        using var test = new TestDatabase();
        test.CurrentUser.Name = null;

        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await new CustomerRepositoryEF(test.Factory).Create(customer);

        var saved = await CustomerAsync(test, customer.Id);
        Assert.NotNull(saved.CreatedAt);
        Assert.Null(saved.CreatedBy);
    }

    [Fact]
    public async Task The_access_check_on_each_screen_tells_saves_who_is_logged_in()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Users.Add(new UserEntity { Name = "Hiten", Password = "x" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var current = new CurrentUser();
        // The login may be typed in other capitals; the stamp uses the name as stored.
        await new AccessService(test.Factory, new FixedLogin("hiten"), current).CurrentAsync();
        Assert.Equal("Hiten", current.Name);

        await new AccessService(test.Factory, new FixedLogin(null), current).CurrentAsync();
        Assert.Null(current.Name);
    }

    private sealed class FixedLogin(string? name) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(name is null
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Test"))));
    }
}
