using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Authentication;

/// <summary>
/// Runs at startup right after <see cref="UserBootstrap"/>. If no roles exist yet, creates the "Admin" role
/// (every permission, now and in future) and gives it permanently to every existing user, so everyone keeps
/// exactly the access they have today. If any role exists it does nothing, so it is safe to run on every start.
/// </summary>
public static class RoleBootstrap
{
    public const string AdminRoleName = "Admin";

    public static async Task RunAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Roles.AnyAsync())
        {
            return;
        }

        // One transaction: a role saved without its users would make the next start skip this step and leave nobody as Admin.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var role = new RoleEntity { Name = AdminRoleName, IsAdmin = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var userIds = await db.Users.Select(u => u.Id).ToListAsync();
        db.UserRoles.AddRange(userIds.Select(id => new UserRoleEntity { UserId = id, RoleId = role.Id }));
        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        logger.LogInformation("Created the Admin role and gave it to {Count} existing user(s).", userIds.Count);
    }
}
