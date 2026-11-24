using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>The rules of the Roles screen (batch 1b-3).</summary>
public class RoleAdminTests
{
    private static RoleAdmin Admin(TestDatabase test) => new(test.Factory);

    private static async Task<int> AddRoleAsync(TestDatabase test, string name, bool isAdmin = false)
    {
        await using var db = test.Open();
        var role = new RoleEntity { Name = name, IsAdmin = isAdmin };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private static async Task GiveRoleAsync(TestDatabase test, string userName, int roleId)
    {
        await using var db = test.Open();
        var user = new UserEntity { Name = userName, Password = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = roleId });
        await db.SaveChangesAsync();
    }

    private static async Task<RoleEntity> LoadAsync(TestDatabase test, int roleId)
    {
        await using var db = test.Open();
        return await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == roleId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_new_role_keeps_its_name_and_switches()
    {
        using var test = new TestDatabase();
        var result = await Admin(test).AddAsync("  Purchase ", new[] { Permissions.ScreenPurchases, Permissions.RatesPurchaseSee });

        Assert.True(result.Ok);
        var row = Assert.Single(await Admin(test).ListAsync());
        Assert.Equal("Purchase", row.Name);
        Assert.False(row.IsAdmin);
        Assert.Equal(0, row.UserCount);
        Assert.Equal(new[] { Permissions.RatesPurchaseSee, Permissions.ScreenPurchases }, row.Permissions.Order());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_role_needs_a_name(string? name)
    {
        using var test = new TestDatabase();
        var result = await Admin(test).AddAsync(name, Array.Empty<string>());
        Assert.False(result.Ok);
        Assert.Equal("Role name is required.", result.Message);
    }

    [Fact]
    public async Task Role_names_are_unique_ignoring_capitals()
    {
        using var test = new TestDatabase();
        await AddRoleAsync(test, "Counter");
        var result = await Admin(test).AddAsync("COUNTER", Array.Empty<string>());
        Assert.False(result.Ok);
        Assert.Contains("already exists", result.Message);
    }

    [Fact]
    public async Task An_unknown_switch_is_refused()
    {
        using var test = new TestDatabase();
        var result = await Admin(test).AddAsync("Odd", new[] { "screen.does-not-exist" });
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Editing_replaces_the_switches_and_may_keep_the_same_name()
    {
        using var test = new TestDatabase();
        await Admin(test).AddAsync("Counter", new[] { Permissions.ScreenSales, Permissions.ScreenCustomers });
        var id = (await Admin(test).ListAsync()).Single().Id;

        var result = await Admin(test).UpdateAsync(id, "Counter", new[] { Permissions.ScreenSales });

        Assert.True(result.Ok);
        var role = await LoadAsync(test, id);
        Assert.Equal(Permissions.ScreenSales, Assert.Single(role.Permissions).Permission);
    }

    [Fact]
    public async Task Renaming_onto_another_roles_name_is_refused()
    {
        using var test = new TestDatabase();
        await AddRoleAsync(test, "Counter");
        var other = await AddRoleAsync(test, "Purchase");
        var result = await Admin(test).UpdateAsync(other, "counter", Array.Empty<string>());
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task The_Admin_role_cannot_be_changed_or_deleted()
    {
        using var test = new TestDatabase();
        var admin = await AddRoleAsync(test, "Admin", isAdmin: true);

        Assert.False((await Admin(test).UpdateAsync(admin, "Boss", Array.Empty<string>())).Ok);
        Assert.False((await Admin(test).DeleteAsync(admin)).Ok);
        Assert.Equal("Admin", (await LoadAsync(test, admin)).Name);
    }

    [Fact]
    public async Task A_role_that_people_hold_cannot_be_deleted()
    {
        using var test = new TestDatabase();
        var counter = await AddRoleAsync(test, "Counter");
        await GiveRoleAsync(test, "Asha", counter);
        await GiveRoleAsync(test, "Ravi", counter);

        var result = await Admin(test).DeleteAsync(counter);

        Assert.False(result.Ok);
        Assert.StartsWith("2 users hold this role", result.Message);
        Assert.Equal(2, (await Admin(test).ListAsync()).Single().UserCount);
    }

    [Fact]
    public async Task An_unused_role_is_deleted_with_its_switches()
    {
        using var test = new TestDatabase();
        await Admin(test).AddAsync("Spare", new[] { Permissions.ScreenReports });
        var id = (await Admin(test).ListAsync()).Single().Id;

        Assert.True((await Admin(test).DeleteAsync(id)).Ok);

        await using var db = test.Open();
        Assert.Empty(await db.Roles.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.RolePermissions.ToListAsync(TestContext.Current.CancellationToken));
    }
}
