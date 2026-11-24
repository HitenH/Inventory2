using Inventory.Authentication;
using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>The rules of the Users screen (batch 1b-2).</summary>
public class UserAdminTests
{
    private static readonly PasswordService Passwords = new();

    private static UserAdmin Admin(TestDatabase test) => new(test.Factory, Passwords);

    private static async Task<int> AddRoleAsync(TestDatabase test, string name, bool isAdmin = false, params string[] keys)
    {
        await using var db = test.Open();
        var role = new RoleEntity { Name = name, IsAdmin = isAdmin };
        foreach (var key in keys)
        {
            role.Permissions.Add(new RolePermissionEntity { Permission = key });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private static async Task<int> AddUserAsync(TestDatabase test, string name, bool disabled = false, params (int RoleId, DateTime? Expires)[] roles)
    {
        await using var db = test.Open();
        var user = new UserEntity { Name = name, Password = "x", IsDisabled = disabled };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(roles.Select(r => new UserRoleEntity { UserId = user.Id, RoleId = r.RoleId, ExpiresAt = r.Expires }));
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static readonly DateTime Future = DateTime.Now.AddDays(30);
    private static readonly DateTime Past = DateTime.Now.AddDays(-1);

    [Fact]
    public async Task Add_user_stores_a_hash_and_the_roles()
    {
        using var test = new TestDatabase();
        var checker = await AddRoleAsync(test, "Checker");
        var result = await Admin(test).AddAsync("  Asha ", "longenough1", new[] { new RoleAssignment(checker, Future) });

        Assert.True(result.Ok);
        await using var db = test.Open();
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Asha", user.Name);
        Assert.NotEqual("longenough1", user.Password);
        Assert.True(Passwords.Verify(user, "longenough1", out _));
        var grant = await db.UserRoles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(checker, grant.RoleId);
        Assert.Equal(Future, grant.ExpiresAt);
    }

    [Fact]
    public async Task Duplicate_name_ignoring_case_is_refused()
    {
        using var test = new TestDatabase();
        await AddUserAsync(test, "Asha");
        var result = await Admin(test).AddAsync("ASHA", "longenough1", Array.Empty<RoleAssignment>());
        Assert.False(result.Ok);
        await using var db = test.Open();
        Assert.Equal(1, await db.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Short_password_and_blank_name_are_refused()
    {
        using var test = new TestDatabase();
        Assert.False((await Admin(test).AddAsync("Asha", "short", Array.Empty<RoleAssignment>())).Ok);
        Assert.False((await Admin(test).AddAsync("   ", "longenough1", Array.Empty<RoleAssignment>())).Ok);
        await using var db = test.Open();
        Assert.Equal(0, await db.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_user_with_a_past_end_date_saves_nothing()
    {
        using var test = new TestDatabase();
        var checker = await AddRoleAsync(test, "Checker");
        var result = await Admin(test).AddAsync("Asha", "longenough1", new[] { new RoleAssignment(checker, Past) });
        Assert.False(result.Ok);
        await using var db = test.Open();
        Assert.Equal(0, await db.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Set_roles_replaces_the_old_set()
    {
        using var test = new TestDatabase();
        var a = await AddRoleAsync(test, "A");
        var b = await AddRoleAsync(test, "B");
        var c = await AddRoleAsync(test, "C");
        var id = await AddUserAsync(test, "Asha", false, (a, null), (b, null));

        var result = await Admin(test).SetRolesAsync(id, new[] { new RoleAssignment(b, Future), new RoleAssignment(c, null) });

        Assert.True(result.Ok);
        await using var db = test.Open();
        var grants = await db.UserRoles.Where(g => g.UserId == id).OrderBy(g => g.RoleId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { b, c }, grants.Select(g => g.RoleId));
        Assert.Equal(Future, grants[0].ExpiresAt);
        Assert.Null(grants[1].ExpiresAt);
    }

    [Fact]
    public async Task Past_end_date_is_refused_and_old_roles_stay()
    {
        using var test = new TestDatabase();
        var a = await AddRoleAsync(test, "A");
        var id = await AddUserAsync(test, "Asha", false, (a, null));

        var result = await Admin(test).SetRolesAsync(id, new[] { new RoleAssignment(a, Past) });

        Assert.False(result.Ok);
        Assert.Equal("The end date must be in the future.", result.Message);
        await using var db = test.Open();
        Assert.Null((await db.UserRoles.SingleAsync(TestContext.Current.CancellationToken)).ExpiresAt);
    }

    [Fact]
    public async Task Reset_password_changes_the_hash_and_the_new_one_verifies()
    {
        using var test = new TestDatabase();
        var id = await AddUserAsync(test, "Asha");
        Assert.False((await Admin(test).ResetPasswordAsync(id, "short")).Ok);

        var result = await Admin(test).ResetPasswordAsync(id, "brandnew99");

        Assert.True(result.Ok);
        await using var db = test.Open();
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual("x", user.Password);
        Assert.True(Passwords.Verify(user, "brandnew99", out _));
        Assert.False(Passwords.Verify(user, "longenough1", out _));
    }

    [Fact]
    public async Task A_user_cannot_disable_themselves()
    {
        using var test = new TestDatabase();
        var admin = await AddRoleAsync(test, "Admin", true);
        var one = await AddUserAsync(test, "One", false, (admin, null));
        await AddUserAsync(test, "Two", false, (admin, null));

        var result = await Admin(test).DisableAsync(one, "one");

        Assert.False(result.Ok);
        await using var db = test.Open();
        Assert.False((await db.Users.SingleAsync(u => u.Id == one, TestContext.Current.CancellationToken)).IsDisabled);
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_disabled_or_lose_the_Admin_role()
    {
        using var test = new TestDatabase();
        var admin = await AddRoleAsync(test, "Admin", true);
        var other = await AddRoleAsync(test, "Checker");
        var only = await AddUserAsync(test, "Only", false, (admin, null));
        var helper = await AddUserAsync(test, "Helper", false, (other, null));

        Assert.False((await Admin(test).DisableAsync(only, "Helper")).Ok);
        Assert.False((await Admin(test).SetRolesAsync(only, new[] { new RoleAssignment(other, null) })).Ok);
        Assert.False((await Admin(test).SetRolesAsync(only, Array.Empty<RoleAssignment>())).Ok);
        // Keeping Admin, with a different end date or extra roles, is fine.
        Assert.True((await Admin(test).SetRolesAsync(only, new[] { new RoleAssignment(admin, null), new RoleAssignment(other, null) })).Ok);
        Assert.True((await Admin(test).DisableAsync(helper, "Only")).Ok);
    }

    [Fact]
    public async Task A_second_active_admin_makes_both_changes_allowed()
    {
        using var test = new TestDatabase();
        var admin = await AddRoleAsync(test, "Admin", true);
        var one = await AddUserAsync(test, "One", false, (admin, null));
        var two = await AddUserAsync(test, "Two", false, (admin, null));

        Assert.True((await Admin(test).SetRolesAsync(one, Array.Empty<RoleAssignment>())).Ok);
        Assert.True((await Admin(test).EnableAsync(one)).Ok);
        await Admin(test).SetRolesAsync(one, new[] { new RoleAssignment(admin, null) });
        Assert.True((await Admin(test).DisableAsync(two, "One")).Ok);
    }

    [Fact]
    public async Task An_expired_admin_grant_or_a_disabled_admin_does_not_count()
    {
        using var test = new TestDatabase();
        var admin = await AddRoleAsync(test, "Admin", true);
        var live = await AddUserAsync(test, "Live", false, (admin, null));
        await AddUserAsync(test, "Expired", false, (admin, Past));
        await AddUserAsync(test, "Disabled", true, (admin, null));

        var result = await Admin(test).DisableAsync(live, "Someone");

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Enable_works()
    {
        using var test = new TestDatabase();
        var id = await AddUserAsync(test, "Asha", true);
        Assert.True((await Admin(test).EnableAsync(id)).Ok);
        await using var db = test.Open();
        Assert.False((await db.Users.SingleAsync(TestContext.Current.CancellationToken)).IsDisabled);
    }

    [Fact]
    public async Task List_shows_users_with_their_roles()
    {
        using var test = new TestDatabase();
        var a = await AddRoleAsync(test, "A");
        await AddUserAsync(test, "Asha", false, (a, Future));

        var rows = await Admin(test).ListAsync();

        var row = Assert.Single(rows);
        Assert.Equal("Asha", row.Name);
        var role = Assert.Single(row.Roles);
        Assert.Equal("A", role.RoleName);
        Assert.Equal(Future, role.ExpiresAt);
    }

    [Fact]
    public async Task May_manage_users_for_Admin_or_the_switch_but_not_otherwise()
    {
        using var test = new TestDatabase();
        var admin = await AddRoleAsync(test, "Admin", true);
        var manager = await AddRoleAsync(test, "Manager", false, Permissions.AdminUsers);
        var plain = await AddRoleAsync(test, "Plain", false, Permissions.ScreenSales);
        await AddUserAsync(test, "A", false, (admin, null));
        await AddUserAsync(test, "M", false, (manager, null));
        await AddUserAsync(test, "P", false, (plain, null));
        await AddUserAsync(test, "D", true, (admin, null));
        await AddUserAsync(test, "E", false, (admin, Past));

        var users = Admin(test);
        Assert.True(await users.MayManageUsersAsync("a"));
        Assert.True(await users.MayManageUsersAsync("M"));
        Assert.False(await users.MayManageUsersAsync("P"));
        Assert.False(await users.MayManageUsersAsync("D"));
        Assert.False(await users.MayManageUsersAsync("E"));
        Assert.False(await users.MayManageUsersAsync("nobody"));
    }
}
