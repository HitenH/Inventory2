namespace Inventory.Service;

/// <summary>One role held by a user. ExpiresAt null means permanent; a date means temporary access (cover for an absent approver).</summary>
public sealed record RoleGrant(bool IsAdmin, IReadOnlySet<string> Permissions, DateTime? ExpiresAt);

/// <summary>
/// Version 3.0, stage 1b: the rule book for "may this person do this?".
/// Pure: no database, no screen, so every device asks the same questions.
/// </summary>
public static class Access
{
    /// <summary>True when any role that has not expired is an Admin role or lists the permission.</summary>
    public static bool Has(IEnumerable<RoleGrant> grants, string permission, DateTime now)
    {
        foreach (var grant in grants)
        {
            if (grant.ExpiresAt is { } expires && expires <= now)
            {
                continue;
            }

            if (grant.IsAdmin || grant.Permissions.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Same as the other Has, but a disabled user has no permissions at all.</summary>
    public static bool Has(bool isDisabled, IEnumerable<RoleGrant> grants, string permission, DateTime now)
        => !isDisabled && Has(grants, permission, now);

    /// <summary>The two permissions in the order they are stored (ordinal, smaller first).</summary>
    public static (string A, string B) NormalisePair(string p1, string p2)
        => string.CompareOrdinal(p1, p2) <= 0 ? (p1, p2) : (p2, p1);

    /// <summary>True when the same person may not do both permissions on the same entry. A permission paired with itself is never blocked.</summary>
    public static bool IsPairBlocked(IEnumerable<(string A, string B)> rules, string p1, string p2)
    {
        if (p1 == p2)
        {
            return false;
        }

        var wanted = NormalisePair(p1, p2);
        return rules.Any(rule => NormalisePair(rule.A, rule.B) == wanted);
    }
}
