using Inventory.Authentication;
using Inventory.Domain.Entities;
using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Line maths used on purchase and sales-order lines, and password handling.</summary>
public class CalculationTests
{
    [Theory]
    [InlineData(10.5, 3, 31.5)]
    [InlineData(0.333, 3, 1.0)]      // rounded to 2 decimals (0.999 -> 1.00)
    [InlineData(0, 5, 0)]
    [InlineData(12, 0, 0)]
    public void Amount_is_rate_times_quantity_rounded(decimal rate, int quantity, decimal expected)
        => Assert.Equal(expected, LineAmounts.ComputeAmount(rate, quantity));

    [Fact]
    public void Amount_is_zero_when_rate_or_quantity_missing()
    {
        Assert.Equal(0, LineAmounts.ComputeAmount(null, 3));
        Assert.Equal(0, LineAmounts.ComputeAmount(5, null));
    }

    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(100, 0, 100)]
    [InlineData(99.99, 15, 84.99)]   // 84.9915 -> 84.99
    [InlineData(100, 100, 0)]
    public void Discount_is_a_percentage_of_the_amount(decimal amount, int discount, decimal expected)
        => Assert.Equal(expected, LineAmounts.ComputeAmountAfterDiscount(amount, discount));

    [Fact]
    public void Discount_on_empty_amount_keeps_it_empty()
    {
        Assert.Null(LineAmounts.ComputeAmountAfterDiscount(null, 10));
        Assert.Equal(0m, LineAmounts.ComputeAmountAfterDiscount(0m, 10));
    }

    [Fact]
    public void Password_hash_verifies_only_the_right_password()
    {
        var service = new PasswordService();
        var user = new UserEntity { Name = "Test" };
        user.Password = service.Hash(user, "correct horse");

        Assert.True(PasswordService.LooksHashed(user.Password));
        Assert.DoesNotContain("correct horse", user.Password);
        Assert.True(service.Verify(user, "correct horse", out _));
        Assert.False(service.Verify(user, "wrong password", out _));
        Assert.False(service.Verify(user, "Correct horse", out _));   // upper/lower case matters
    }

    [Fact]
    public void Plain_text_password_is_not_mistaken_for_a_hash()
        => Assert.False(PasswordService.LooksHashed("password123"));

    [Theory]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("short", false)]
    [InlineData("long enough", true)]
    public void New_password_rules(string? password, bool accepted)
        => Assert.Equal(accepted, PasswordService.ValidateNewPassword(password) is null);
}
