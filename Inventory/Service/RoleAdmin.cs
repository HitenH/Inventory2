using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

public sealed record RoleRow(int Id, string Name, bool IsAdmin, int UserCount, IReadOnlyList<string> Permissions);

/// <summary>
/// Version 3.0, batch 1b-3: every rule about adding, changing and deleting roles. The Roles screen only asks.
/// The Admin role always has everything, so it can never be changed or deleted.
/// </summary>
public class RoleAdmin(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<RoleRow>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync();
        db.MarkSeen(roles);   // batch 1c-2: the versions this screen shows
        var holders = await db.UserRoles.AsNoTracking().GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() }).ToListAsync();
        return roles.Select(r => new RoleRow(r.Id, r.Name, r.IsAdmin,
            holders.FirstOrDefault(h => h.RoleId == r.Id)?.Count ?? 0,
            r.Permissions.Select(p => p.Permission).ToList())).ToList();
    }

    public async Task<UserAdminResult> AddAsync(string? name, IReadOnlyCollection<string> permissions)
    {
        await using var db = await factory.CreateDbContextAsync();
        var problem = await CheckAsync(db, null, name, permissions);
        if (problem is not null)
        {
            return UserAdminResult.Refuse(problem);
        }

        var role = new RoleEntity { Name = name!.Trim() };
        foreach (var key in permissions.Distinct())
        {
            role.Permissions.Add(new RolePermissionEntity { Permission = key });
        }

        db.Roles.Add(role);
        if (await UserAdmin.TrySaveAsync(db) is { } refused)
        {
            return refused;
        }
        return UserAdminResult.Success($"Role {role.Name} added.");
    }

    public async Task<UserAdminResult> UpdateAsync(int roleId, string? name, IReadOnlyCollection<string> permissions)
    {
        await using var db = await factory.CreateDbContextAsync();
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId);
        if (role is null)
        {
            return UserAdminResult.Refuse("That role was not found.");
        }

        if (role.IsAdmin)
        {
            return UserAdminResult.Refuse("The Admin role always has everything and cannot be changed.");
        }

        var problem = await CheckAsync(db, roleId, name, permissions);
        if (problem is not null)
        {
            return UserAdminResult.Refuse(problem);
        }

        role.Name = name!.Trim();
        role.Permissions.Clear();
        foreach (var key in permissions.Distinct())
        {
            role.Permissions.Add(new RolePermissionEntity { RoleId = role.Id, Permission = key });
        }

        if (await UserAdmin.TrySaveAsync(db) is { } refused)
        {
            return refused;
        }
        return UserAdminResult.Success($"Role {role.Name} saved.");
    }

    public async Task<UserAdminResult> DeleteAsync(int roleId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (role is null)
        {
            return UserAdminResult.Refuse("That role was not found.");
        }

        if (role.IsAdmin)
        {
            return UserAdminResult.Refuse("The Admin role cannot be deleted.");
        }

        // Owner's decision (25 Nov 2026): refuse rather than quietly take the role off people,
        // which could leave someone with no access without anyone noticing.
        var holders = await db.UserRoles.CountAsync(ur => ur.RoleId == roleId);
        if (holders > 0)
        {
            return UserAdminResult.Refuse(holders == 1
                ? "1 user holds this role. Take it off them first (Users screen)."
                : $"{holders} users hold this role. Take it off them first (Users screen).");
        }

        db.Roles.Remove(role);
        if (await UserAdmin.TrySaveAsync(db) is { } refused)
        {
            return refused;
        }
        return UserAdminResult.Success($"Role {role.Name} deleted.");
    }

    private static async Task<string?> CheckAsync(AppDbContext db, int? roleId, string? name, IReadOnlyCollection<string> permissions)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return "Role name is required.";
        }

        var names = await db.Roles.AsNoTracking().Where(r => r.Id != roleId).Select(r => r.Name).ToListAsync();
        if (names.Any(n => string.Equals(n.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return $"A role named \"{trimmed}\" already exists.";
        }

        var known = Permissions.All.Select(p => p.Key).ToHashSet();
        if (permissions.Any(p => !known.Contains(p)))
        {
            return "One of the chosen switches is not known to this version of the app.";
        }

        return null;
    }
}
