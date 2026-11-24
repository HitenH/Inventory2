using Inventory.Authentication;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

public sealed record UserRoleView(int RoleId, string RoleName, DateTime? ExpiresAt);

public sealed record UserRow(int Id, string Name, bool IsDisabled, IReadOnlyList<UserRoleView> Roles);

public sealed record RoleChoice(int Id, string Name);

/// <summary>One role to give a user. ExpiresAt null = always; a date = temporary cover.</summary>
public sealed record RoleAssignment(int RoleId, DateTime? ExpiresAt);

/// <summary>A rule break is a message for the operator, never an exception.</summary>
public sealed record UserAdminResult(bool Ok, string Message)
{
    public static UserAdminResult Success(string message = "") => new(true, message);
    public static UserAdminResult Refuse(string message) => new(false, message);
}

/// <summary>
/// Version 3.0, batch 1b-2: every rule about adding users, giving them roles, resetting passwords and
/// disabling them. The Users screen only asks. Users are never deleted (disable keeps the history).
/// </summary>
public class UserAdmin(IDbContextFactory<AppDbContext> factory, PasswordService passwords)
{
    public async Task<List<UserRow>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync();
        var grants = await (from ur in db.UserRoles.AsNoTracking()
                            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                            select new { ur.UserId, ur.RoleId, r.Name, ur.ExpiresAt }).ToListAsync();
        return users.Select(u => new UserRow(u.Id, u.Name, u.IsDisabled,
            grants.Where(g => g.UserId == u.Id).OrderBy(g => g.Name)
                  .Select(g => new UserRoleView(g.RoleId, g.Name, g.ExpiresAt)).ToList())).ToList();
    }

    public async Task<List<RoleChoice>> ListRolesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => new RoleChoice(r.Id, r.Name)).ToListAsync();
    }

    /// <summary>True when the named user is active and holds an unexpired role with the "manage users" switch.</summary>
    public async Task<bool> MayManageUsersAsync(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        await using var db = await factory.CreateDbContextAsync();
        var name = userName.Trim().ToLower();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Name.ToLower() == name);
        if (user is null)
        {
            return false;
        }

        var roles = await (from ur in db.UserRoles.AsNoTracking()
                           join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                           where ur.UserId == user.Id
                           select new { ur.ExpiresAt, r.IsAdmin, r.Id }).ToListAsync();
        var keys = await db.RolePermissions.AsNoTracking().ToListAsync();
        var grants = roles.Select(r => new RoleGrant(r.IsAdmin,
            keys.Where(k => k.RoleId == r.Id).Select(k => k.Permission).ToHashSet(), r.ExpiresAt));
        return Access.Has(user.IsDisabled, grants, Permissions.AdminUsers, DateTime.Now);
    }

    public async Task<UserAdminResult> AddAsync(string? name, string? password, IReadOnlyList<RoleAssignment> roles)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return UserAdminResult.Refuse("Name is required.");
        }

        var passwordProblem = PasswordService.ValidateNewPassword(password);
        if (passwordProblem is not null)
        {
            return UserAdminResult.Refuse(passwordProblem);
        }

        await using var db = await factory.CreateDbContextAsync();
        var names = await db.Users.AsNoTracking().Select(u => u.Name).ToListAsync();
        if (names.Any(n => string.Equals(n?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            return UserAdminResult.Refuse($"A user named \"{name}\" already exists.");
        }

        var rolesProblem = await CheckAssignmentsAsync(db, roles);
        if (rolesProblem is not null)
        {
            return UserAdminResult.Refuse(rolesProblem);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new UserEntity { Name = name, Password = string.Empty, IsDisabled = false };
        user.Password = passwords.Hash(user, password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.AddRange(roles.Select(r => new UserRoleEntity { UserId = user.Id, RoleId = r.RoleId, ExpiresAt = r.ExpiresAt }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UserAdminResult.Success($"User {name} added.");
    }

    public async Task<UserAdminResult> SetRolesAsync(int userId, IReadOnlyList<RoleAssignment> roles)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return UserAdminResult.Refuse("That user was not found.");
        }

        var rolesProblem = await CheckAssignmentsAsync(db, roles);
        if (rolesProblem is not null)
        {
            return UserAdminResult.Refuse(rolesProblem);
        }

        var now = DateTime.Now;
        if (!user.IsDisabled && await IsAdminNowAsync(db, userId, now))
        {
            var adminRoleIds = await db.Roles.Where(r => r.IsAdmin).Select(r => r.Id).ToListAsync();
            var keepsAdmin = roles.Any(r => adminRoleIds.Contains(r.RoleId) && (r.ExpiresAt is null || r.ExpiresAt > now));
            if (!keepsAdmin && await OtherActiveAdminsAsync(db, userId, now) == 0)
            {
                return UserAdminResult.Refuse("This is the last active Admin. Give another user the Admin role first.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.UserRoles.RemoveRange(await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync());
        db.UserRoles.AddRange(roles.Select(r => new UserRoleEntity { UserId = userId, RoleId = r.RoleId, ExpiresAt = r.ExpiresAt }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UserAdminResult.Success($"Roles saved for {user.Name}.");
    }

    public async Task<UserAdminResult> ResetPasswordAsync(int userId, string? newPassword)
    {
        var problem = PasswordService.ValidateNewPassword(newPassword);
        if (problem is not null)
        {
            return UserAdminResult.Refuse(problem);
        }

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return UserAdminResult.Refuse("That user was not found.");
        }

        user.Password = passwords.Hash(user, newPassword!);
        await db.SaveChangesAsync();
        return UserAdminResult.Success($"Password changed for {user.Name}.");
    }

    public async Task<UserAdminResult> DisableAsync(int userId, string? actingUserName)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return UserAdminResult.Refuse("That user was not found.");
        }

        if (!string.IsNullOrWhiteSpace(actingUserName)
            && string.Equals(user.Name?.Trim(), actingUserName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return UserAdminResult.Refuse("You cannot disable yourself.");
        }

        var now = DateTime.Now;
        if (!user.IsDisabled && await IsAdminNowAsync(db, userId, now) && await OtherActiveAdminsAsync(db, userId, now) == 0)
        {
            return UserAdminResult.Refuse("This is the last active Admin and cannot be disabled.");
        }

        user.IsDisabled = true;
        await db.SaveChangesAsync();
        return UserAdminResult.Success($"{user.Name} is disabled.");
    }

    public async Task<UserAdminResult> EnableAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return UserAdminResult.Refuse("That user was not found.");
        }

        user.IsDisabled = false;
        await db.SaveChangesAsync();
        return UserAdminResult.Success($"{user.Name} is enabled.");
    }

    private static async Task<string?> CheckAssignmentsAsync(AppDbContext db, IReadOnlyList<RoleAssignment> roles)
    {
        if (roles.Select(r => r.RoleId).Distinct().Count() != roles.Count)
        {
            return "The same role was chosen twice.";
        }

        var now = DateTime.Now;
        if (roles.Any(r => r.ExpiresAt is { } end && end <= now))
        {
            return "The end date must be in the future.";
        }

        var ids = roles.Select(r => r.RoleId).ToList();
        if (await db.Roles.CountAsync(r => ids.Contains(r.Id)) != ids.Count)
        {
            return "One of the chosen roles no longer exists.";
        }

        return null;
    }

    // An Admin grant counts only while it has not expired.
    private static async Task<bool> IsAdminNowAsync(AppDbContext db, int userId, DateTime now)
        => await ActiveAdminIdsAsync(db, now, userId) is { Count: > 0 };

    private static async Task<int> OtherActiveAdminsAsync(AppDbContext db, int userId, DateTime now)
        => (await ActiveAdminIdsAsync(db, now, null)).Count(id => id != userId);

    private static async Task<List<int>> ActiveAdminIdsAsync(AppDbContext db, DateTime now, int? onlyUserId)
    {
        var query = from ur in db.UserRoles
                    join r in db.Roles on ur.RoleId equals r.Id
                    join u in db.Users on ur.UserId equals u.Id
                    where r.IsAdmin && (ur.ExpiresAt == null || ur.ExpiresAt > now) && !u.IsDisabled
                    select ur.UserId;
        if (onlyUserId is { } id)
        {
            query = query.Where(x => x == id);
        }

        return await query.Distinct().ToListAsync();
    }
}
