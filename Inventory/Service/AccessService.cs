using Inventory.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>What one person may do, read fresh from the database (roles changed by the Admin apply on the next screen).</summary>
public sealed class UserAccess(string? name, bool signedIn, bool found, bool isDisabled, IReadOnlyList<RoleGrant> grants, DateTime now)
{
    /// <summary>Not logged in.</summary>
    public static readonly UserAccess Nobody = new(null, false, false, false, Array.Empty<RoleGrant>(), DateTime.MinValue);

    public string? Name { get; } = name;
    public bool SignedIn { get; } = signedIn;
    public bool Found { get; } = found;
    public bool IsDisabled { get; } = isDisabled;

    public bool Allows(string permission) => Found && Access.Has(IsDisabled, grants, permission, now);

    /// <summary>True when the address may be opened (null = open to everyone who is logged in).</summary>
    public bool AllowsPath(string path) => Permissions.ForPath(path) is not { } needed || Allows(needed);
}

/// <summary>Version 3.0, batch 1b-4: loads the logged-in person's roles so screens and the menu can ask <see cref="UserAccess.Allows"/>.</summary>
public class AccessService(IDbContextFactory<AppDbContext> factory, AuthenticationStateProvider auth)
{
    public async Task<UserAccess> CurrentAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            return UserAccess.Nobody;
        }

        await using var db = await factory.CreateDbContextAsync();
        return await LoadAsync(db, state.User.Identity.Name, DateTime.Now);
    }

    public static async Task<UserAccess> LoadAsync(AppDbContext db, string? userName, DateTime now)
    {
        // Logged in, but no such user (e.g. a blank name): signed in with nothing allowed.
        var unknown = new UserAccess(userName, true, false, false, Array.Empty<RoleGrant>(), now);
        if (string.IsNullOrWhiteSpace(userName))
        {
            return unknown;
        }

        var name = userName.Trim().ToLower();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Name.ToLower() == name);
        if (user is null)
        {
            return unknown;
        }

        var roles = await (from ur in db.UserRoles.AsNoTracking()
                           join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                           where ur.UserId == user.Id
                           select new { r.Id, r.IsAdmin, ur.ExpiresAt }).ToListAsync();
        var roleIds = roles.Select(r => r.Id).ToList();
        var keys = await db.RolePermissions.AsNoTracking().Where(k => roleIds.Contains(k.RoleId)).ToListAsync();
        var grants = roles.Select(r => new RoleGrant(r.IsAdmin,
            keys.Where(k => k.RoleId == r.Id).Select(k => k.Permission).ToHashSet(), r.ExpiresAt)).ToList();
        return new UserAccess(user.Name, true, true, user.IsDisabled, grants, now);
    }
}
