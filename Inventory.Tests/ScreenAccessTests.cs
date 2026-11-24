using System.Reflection;
using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.AspNetCore.Components;

namespace Inventory.Tests;

/// <summary>Batch 1b-4: which switch opens which screen, and what a person's roles allow.</summary>
public class ScreenAccessTests
{
    [Theory]
    [InlineData("/", Permissions.ScreenDashboard)]
    [InlineData("/sale/3f2a?x=1", Permissions.ScreenSales)]
    [InlineData("/salessummary/abc", Permissions.ScreenSales)]
    [InlineData("/SALES", Permissions.ScreenSales)]
    [InlineData("/purchase", Permissions.ScreenPurchases)]
    [InlineData("/purchaseorder", Permissions.ScreenPurchaseOrders)]
    [InlineData("/salesorder/1", Permissions.ScreenSalesOrders)]
    [InlineData("/category", Permissions.ScreenProducts)]
    [InlineData("/roles", Permissions.AdminUsers)]
    [InlineData("/settings", Permissions.AdminSettings)]
    [InlineData("/something-new", Permissions.Unlisted)]
    public void Each_address_needs_its_switch(string path, string expected)
        => Assert.Equal(expected, Permissions.ForPath(path));

    [Theory]
    [InlineData("/login")]
    [InlineData("/login?ReturnUrl=sales")]
    [InlineData("/changepassword")]
    public void Login_and_change_password_are_open_to_everyone(string path)
        => Assert.Null(Permissions.ForPath(path));

    [Fact]
    public void Every_screen_of_the_app_is_listed()
    {
        // A new screen must be added to Permissions.ForPath; until then only an Admin can open it.
        var templates = typeof(Permissions).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
            .Select(r => r.Template)
            .ToList();

        Assert.NotEmpty(templates);
        var unlisted = templates.Where(t => Permissions.ForPath(t.Split('{')[0]) == Permissions.Unlisted).ToList();
        Assert.Empty(unlisted);
    }

    private static async Task<UserAccess> SetUpAsync(TestDatabase test, bool disabled, bool isAdmin, DateTime? expires, params string[] keys)
    {
        await using (var db = test.Open())
        {
            var role = new RoleEntity { Name = "R", IsAdmin = isAdmin };
            foreach (var key in keys)
            {
                role.Permissions.Add(new RolePermissionEntity { Permission = key });
            }

            var user = new UserEntity { Name = "Asha", Password = "x", IsDisabled = disabled };
            db.Roles.Add(role);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id, ExpiresAt = expires });
            await db.SaveChangesAsync();
        }

        await using var read = test.Open();
        return await AccessService.LoadAsync(read, " asha ", DateTime.Now);
    }

    [Fact]
    public async Task A_role_opens_only_its_own_screens()
    {
        using var test = new TestDatabase();
        var access = await SetUpAsync(test, false, false, null, Permissions.ScreenSales);

        Assert.True(access.AllowsPath("/sale/1"));
        Assert.True(access.AllowsPath("/login"));
        Assert.False(access.AllowsPath("/purchases"));
        Assert.False(access.AllowsPath("/"));
        Assert.False(access.Allows(Permissions.AdminImport));
    }

    [Fact]
    public async Task Admin_opens_everything_including_unlisted_screens()
    {
        using var test = new TestDatabase();
        var access = await SetUpAsync(test, false, true, null);

        Assert.True(access.AllowsPath("/settings"));
        Assert.True(access.AllowsPath("/something-new"));
    }

    [Fact]
    public async Task A_disabled_user_opens_nothing_but_login()
    {
        using var test = new TestDatabase();
        var access = await SetUpAsync(test, true, true, null);

        Assert.True(access.IsDisabled);
        Assert.False(access.AllowsPath("/sales"));
        Assert.True(access.AllowsPath("/login"));
    }

    [Fact]
    public async Task An_expired_temporary_role_opens_nothing()
    {
        using var test = new TestDatabase();
        var access = await SetUpAsync(test, false, false, DateTime.Now.AddMinutes(-1), Permissions.ScreenSales);
        Assert.False(access.AllowsPath("/sales"));
    }

    [Fact]
    public async Task An_unknown_name_is_signed_in_with_nothing_allowed()
    {
        using var test = new TestDatabase();
        await using var db = test.Open();
        var access = await AccessService.LoadAsync(db, "Nobody", DateTime.Now);

        Assert.True(access.SignedIn);
        Assert.False(access.Found);
        Assert.False(access.AllowsPath("/"));
    }
}
