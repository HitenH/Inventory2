using Inventory.Domain;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>What one person may do, read fresh from the database (roles changed by the Admin apply on the next screen).</summary>
public sealed class UserAccess(string? name, bool signedIn, bool found, bool isDisabled, IReadOnlyList<RoleGrant> grants, DateTime now,
    IReadOnlySet<int>? places = null)
{
    /// <summary>Not logged in.</summary>
    public static readonly UserAccess Nobody = new(null, false, false, false, Array.Empty<RoleGrant>(), DateTime.MinValue);

    public string? Name { get; } = name;
    public bool SignedIn { get; } = signedIn;
    public bool Found { get; } = found;
    public bool IsDisabled { get; } = isDisabled;

    public bool Allows(string permission) => Found && Access.Has(IsDisabled, grants, permission, now);

    /// <summary>Batch 1c-5: holds an Admin role that has not expired (only the Admin role may erase the activity log).</summary>
    public bool HasAdminRole => Found && !IsDisabled && grants.Any(g => g.IsAdmin && (g.ExpiresAt is null || g.ExpiresAt > now));

    /// <summary>
    /// Stage 3, batch 3-3 (Hiten, 26 Nov 2026): the places this person works in and sees stock of ("a godown person sees only
    /// godown stock, a manager both"); null = every place (no places ticked on the Users screen, or the Admin role).
    /// </summary>
    public IReadOnlySet<int>? Places => HasAdminRole ? null : places;

    /// <summary>True when this person works in (and sees the stock of) the place.</summary>
    public bool SeesPlace(int placeId) => Places is null || Places.Contains(placeId);

    public bool SeesPurchaseRates => Allows(Permissions.RatesPurchaseSee);
    public bool SeesSaleRates => Allows(Permissions.RatesSaleSee);

    /// <summary>Rates and amounts of this kind of voucher: purchases and purchase orders are purchase rates, sales orders sale rates.</summary>
    public bool SeesRates(BulkEntryKind kind) => kind == BulkEntryKind.SalesOrder ? SeesSaleRates : SeesPurchaseRates;

    /// <summary>True when the address may be opened (null = open to everyone who is logged in).</summary>
    public bool AllowsPath(string path) => Permissions.ForPath(path) is not { } needed || Allows(needed);
}

/// <summary>Version 3.0, batch 1b-4: loads the logged-in person's roles so screens and the menu can ask <see cref="UserAccess.Allows"/>.</summary>
public class AccessService(IDbContextFactory<AppDbContext> factory, AuthenticationStateProvider auth, CurrentUser currentUser)
{
    public async Task<UserAccess> CurrentAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            currentUser.Name = null;
            return UserAccess.Nobody;
        }

        await using var db = await factory.CreateDbContextAsync();
        var access = await LoadAsync(db, state.User.Identity.Name, DateTime.Now);
        // Batch 1c-1: saves made from this tab are stamped with this name (Domain/RecordStamps.cs).
        currentUser.Name = access.Found ? access.Name : null;
        return access;
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
        var places = await db.UserPlaces.AsNoTracking().Where(p => p.UserId == user.Id).Select(p => p.PlaceId).ToListAsync();
        return new UserAccess(user.Name, true, true, user.IsDisabled, grants, now, places.Count == 0 ? null : places.ToHashSet());
    }
}
