using System.Security.Cryptography;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Authentication;

/// <summary>
/// Runs once at startup, after migrations:
/// 1. Any user whose Password column still holds a plain-text value is upgraded to a hash in place
///    (keeps existing logins working on the first run after this change).
/// 2. If the Users table is empty (fresh database), an "Admin" account is created with a random
///    password that is printed to the console and written to initial-admin-password.txt next to
///    the database. Change it immediately after the first login.
/// </summary>
public static class UserBootstrap
{
    public const string InitialPasswordFileName = "initial-admin-password.txt";

    public static async Task RunAsync(AppDbContext db, PasswordService passwords, ILogger logger, string dataDirectory)
    {
        var users = await db.Users.ToListAsync();

        var upgraded = 0;
        foreach (var user in users)
        {
            if (!PasswordService.LooksHashed(user.Password))
            {
                user.Password = passwords.Hash(user, user.Password ?? string.Empty);
                upgraded++;
            }
        }

        if (upgraded > 0)
        {
            await db.SaveChangesAsync();
            logger.LogWarning("Upgraded {Count} user password(s) from plain text to hashed storage.", upgraded);
        }

        if (users.Count == 0)
        {
            var initialPassword = GeneratePassword(14);
            var admin = new UserEntity { Name = "Admin", Role = "Admin", Password = string.Empty };
            admin.Password = passwords.Hash(admin, initialPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            var message = $"No users existed. Created user 'Admin' with password: {initialPassword}  (change it after first login)";
            logger.LogWarning("{Message}", message);
            Console.WriteLine();
            Console.WriteLine("==========================================================");
            Console.WriteLine(message);
            Console.WriteLine("==========================================================");
            Console.WriteLine();

            try
            {
                Directory.CreateDirectory(dataDirectory);
                await File.WriteAllTextAsync(Path.Combine(dataDirectory, InitialPasswordFileName), message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not write {File}", InitialPasswordFileName);
            }
        }
    }

    private static string GeneratePassword(int length)
    {
        // Letters and digits only, excluding look-alikes (0/O, 1/l/I), so it can be read from a console.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(chars);
    }
}
