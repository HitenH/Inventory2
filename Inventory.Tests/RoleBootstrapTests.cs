using Inventory.Authentication;
using Inventory.Domain.Entities;
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
    public async Task Existing_roles_are_left_untouched()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Users.Add(new UserEntity { Name = "One", Password = "x" });
            db.Roles.Add(new RoleEntity { Name = "Counter" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await RoleBootstrap.RunAsync(db, NullLogger.Instance);
        }

        await using var check = test.Open();
        Assert.Equal("Counter", (await check.Roles.SingleAsync(TestContext.Current.CancellationToken)).Name);
        Assert.Empty(await check.UserRoles.ToListAsync(TestContext.Current.CancellationToken));
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
