using Inventory.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Authentication;

/// <summary>
/// Hashes and verifies user passwords using ASP.NET Core's PasswordHasher (PBKDF2, salted).
/// The hash is stored in UserEntity.Password. Plain-text values are never stored.
/// </summary>
public class PasswordService
{
    private readonly PasswordHasher<UserEntity> hasher = new();

    public const int MinimumLength = 8;

    public string Hash(UserEntity user, string plainPassword)
    {
        return hasher.HashPassword(user, plainPassword);
    }

    /// <summary>Returns true when the password matches. rehashNeeded is true when the stored hash uses an older format.</summary>
    public bool Verify(UserEntity user, string plainPassword, out bool rehashNeeded)
    {
        rehashNeeded = false;
        if (string.IsNullOrEmpty(user.Password) || plainPassword is null)
        {
            return false;
        }

        var result = hasher.VerifyHashedPassword(user, user.Password, plainPassword);
        rehashNeeded = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }

    /// <summary>
    /// PasswordHasher output is Base64 of a versioned binary blob and always starts with "AQAAAA"
    /// (version byte 0x01 followed by zero bytes). Anything else is treated as a legacy plain-text value.
    /// </summary>
    public static bool LooksHashed(string? stored)
    {
        return stored is not null && stored.Length >= 60 && stored.StartsWith("AQAAAA", StringComparison.Ordinal);
    }

    public static string? ValidateNewPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (password.Length < MinimumLength)
        {
            return $"Password must be at least {MinimumLength} characters.";
        }

        return null;
    }
}
