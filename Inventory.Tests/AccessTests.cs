using Inventory.Service;

namespace Inventory.Tests;

/// <summary>The rule book for roles and permissions, and the permission list itself.</summary>
public class AccessTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0);

    private static RoleGrant Plain(DateTime? expires = null, params string[] keys)
        => new(false, new HashSet<string>(keys), expires);

    [Fact]
    public void Admin_role_has_every_permission_including_unknown_future_ones()
    {
        var grants = new[] { new RoleGrant(true, new HashSet<string>(), null) };
        Assert.True(Access.Has(grants, Permissions.ScreenSales, Now));
        Assert.True(Access.Has(grants, "some.future.switch", Now));
    }

    [Fact]
    public void Plain_role_has_only_its_permissions()
    {
        var grants = new[] { Plain(null, Permissions.ScreenSales) };
        Assert.True(Access.Has(grants, Permissions.ScreenSales, Now));
        Assert.False(Access.Has(grants, Permissions.ScreenReports, Now));
        Assert.False(Access.Has(Array.Empty<RoleGrant>(), Permissions.ScreenSales, Now));
    }

    [Fact]
    public void Expired_temporary_grant_gives_nothing_and_unexpired_gives_access()
    {
        Assert.False(Access.Has(new[] { Plain(Now.AddMinutes(-1), Permissions.ScreenSales) }, Permissions.ScreenSales, Now));
        Assert.False(Access.Has(new[] { new RoleGrant(true, new HashSet<string>(), Now) }, Permissions.ScreenSales, Now));
        Assert.True(Access.Has(new[] { Plain(Now.AddMinutes(1), Permissions.ScreenSales) }, Permissions.ScreenSales, Now));
    }

    [Fact]
    public void Another_unexpired_role_still_counts_when_one_has_expired()
    {
        var grants = new[] { Plain(Now.AddDays(-1), Permissions.ScreenSales), Plain(null, Permissions.ScreenSales) };
        Assert.True(Access.Has(grants, Permissions.ScreenSales, Now));
    }

    [Fact]
    public void Disabled_user_has_nothing()
    {
        var grants = new[] { new RoleGrant(true, new HashSet<string>(), null) };
        Assert.False(Access.Has(true, grants, Permissions.ScreenSales, Now));
        Assert.True(Access.Has(false, grants, Permissions.ScreenSales, Now));
    }

    [Fact]
    public void Pair_rule_is_order_independent()
    {
        var rules = new[] { Access.NormalisePair("b.second", "a.first") };
        Assert.True(Access.IsPairBlocked(rules, "a.first", "b.second"));
        Assert.True(Access.IsPairBlocked(rules, "b.second", "a.first"));
        Assert.False(Access.IsPairBlocked(rules, "a.first", "c.other"));
    }

    [Fact]
    public void Permission_paired_with_itself_is_never_blocked()
    {
        var rules = new[] { ("a.first", "a.first") };
        Assert.False(Access.IsPairBlocked(rules, "a.first", "a.first"));
    }

    [Fact]
    public void NormalisePair_puts_the_smaller_key_first()
    {
        Assert.Equal(("a", "b"), Access.NormalisePair("b", "a"));
        Assert.Equal(("a", "b"), Access.NormalisePair("a", "b"));
        Assert.Equal(("B", "a"), Access.NormalisePair("a", "B"));
    }

    [Fact]
    public void Every_permission_key_is_unique_and_not_blank()
    {
        Assert.NotEmpty(Permissions.All);
        Assert.All(Permissions.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Key));
            Assert.False(string.IsNullOrWhiteSpace(p.Label));
            Assert.False(string.IsNullOrWhiteSpace(p.Group));
        });
        Assert.Equal(Permissions.All.Count, Permissions.All.Select(p => p.Key).Distinct().Count());
    }
}
