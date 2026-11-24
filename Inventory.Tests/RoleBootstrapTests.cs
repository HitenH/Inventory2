using Inventory.Authentication;
using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.Tests;

/// <summary>The first-run setup of the Admin role.</summary>
public class RoleBootstrapTests
{
    [Fact]
    public async Task Existing_users_all_get_the_Admin_role()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Users.AddRange(new UserEntity { Name = "One", Password = "x" }, new UserEntity { Name = "Two", Password = "x" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        }

        await using var check = test.Open();
        var role = await check.Roles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Admin", role.Name);
        Assert.True(role.IsAdmin);
        var userIds = await check.Users.Select(u => u.Id).ToListAsync(TestContext.Current.CancellationToken);
        var grants = await check.UserRoles.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, grants.Count);
        Assert.All(grants, g => Assert.Null(g.ExpiresAt));
        Assert.Equal(userIds.Order(), grants.Select(g => g.UserId).Order());
    }

    [Fact]
    public async Task Second_run_changes_nothing()
    {
        using var test = new TestDatabase();
        await using var db = test.Open();
        db.Users.Add(new UserEntity { Name = "One", Password = "x" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        await RoleBootstrap.RunAsync(db, NullLogger.Instance);

        await using var check = test.Open();
        Assert.Equal(1, await check.Roles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await check.UserRoles.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Empty_database_gets_an_Admin_user_holding_the_Admin_role()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            await UserBootstrap.RunAsync(db, new PasswordService(), NullLogger.Instance, test.Folder);
            await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        }

        await using var check = test.Open();
        var user = await check.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Admin", user.Name);
        Assert.False(user.IsDisabled);
        var grant = await check.UserRoles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(user.Id, grant.UserId);
        Assert.True((await check.Roles.SingleAsync(r => r.Id == grant.RoleId, TestContext.Current.CancellationToken)).IsAdmin);
    }

    [Fact]
    public async Task An_existing_Admin_role_means_nothing_changes()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Users.Add(new UserEntity { Name = "One", Password = "x" });
            db.Roles.Add(new RoleEntity { Name = "Boss", IsAdmin = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        }

        await using var check = test.Open();
        Assert.Equal("Boss", (await check.Roles.SingleAsync(TestContext.Current.CancellationToken)).Name);
        Assert.Empty(await check.UserRoles.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_upgrade_adds_three_starter_roles_with_all_their_switches()
    {
        using var test = new TestDatabase(keepStarterRoles: true);
        await using var db = test.Open();
        var roles = await db.Roles.Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Purchase", "Reports", "Sales" }, roles.Select(r => r.Name));
        Assert.All(roles, r => Assert.False(r.IsAdmin));
        var purchase = roles[0].Permissions.Select(p => p.Permission).Order().ToList();
        Assert.Equal(new[] { Permissions.RatesPurchaseSee, Permissions.ScreenDashboard, Permissions.ScreenProducts,
            Permissions.ScreenPurchaseOrders, Permissions.ScreenPurchases, Permissions.ScreenSuppliers }.Order(), purchase);
        Assert.DoesNotContain(Permissions.RatesSaleSee, purchase);
        var sales = roles[2].Permissions.Select(p => p.Permission).ToList();
        Assert.Contains(Permissions.RatesSaleSee, sales);
        Assert.DoesNotContain(Permissions.RatesPurchaseSee, sales);
        Assert.Equal(2, roles[1].Permissions.Count);
        Assert.All(roles.SelectMany(r => r.Permissions), p => Assert.Contains(p.Permission, Permissions.All.Select(a => a.Key)));
    }

    [Fact]
    public async Task After_the_upgrade_existing_users_still_become_Admin()
    {
        // The real order on the counter PC: upgrades (starter roles) first, then the start-up step.
        using var test = new TestDatabase(keepStarterRoles: true);
        await using (var db = test.Open())
        {
            db.Users.Add(new UserEntity { Name = "Hiten", Password = "x" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        }

        await using var check = test.Open();
        var grant = await check.UserRoles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True((await check.Roles.SingleAsync(r => r.Id == grant.RoleId, TestContext.Current.CancellationToken)).IsAdmin);
    }

    [Fact]
    public async Task Starter_roles_without_an_Admin_role_still_make_everyone_Admin()
    {
        // The counter PC upgrading from version 2.0: the StarterRoles upgrade has added Purchase, Sales and Reports,
        // but no Admin role exists yet. Everyone must still end up as Admin, or nobody could open anything.
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Users.Add(new UserEntity { Name = "Hiten", Password = "x" });
            db.Roles.Add(new RoleEntity { Name = "Counter" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        }

        await using var check = test.Open();
        var admin = await check.Roles.SingleAsync(r => r.IsAdmin, TestContext.Current.CancellationToken);
        var grant = await check.UserRoles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(admin.Id, grant.RoleId);
    }

    [Fact]
    public async Task Role_names_are_unique_ignoring_case()
    {
        using var test = new TestDatabase();
        await using var db = test.Open();
        db.Roles.Add(new RoleEntity { Name = "Counter" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Roles.Add(new RoleEntity { Name = "COUNTER" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
