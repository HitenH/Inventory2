using Inventory.Authentication;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Inventory.Pages;

public partial class ChangePassword
{
    [Inject] private IUserRepository UserRepository { get; set; } = default!;
    [Inject] private PasswordService Passwords { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ILogger<ChangePassword> Logger { get; set; } = default!;

    private readonly ChangePasswordModel model = new();
    private string userName = string.Empty;
    private string? errorMessage;
    private string? successMessage;
    private bool isBusy;

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        userName = state.User.Identity?.Name ?? string.Empty;
    }

    private async Task Submit()
    {
        errorMessage = null;
        successMessage = null;

        var validation = PasswordService.ValidateNewPassword(model.NewPassword);
        if (validation is not null)
        {
            errorMessage = validation;
            return;
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            errorMessage = "The new passwords do not match.";
            return;
        }

        if (model.NewPassword == model.CurrentPassword)
        {
            errorMessage = "The new password must be different from the current one.";
            return;
        }

        isBusy = true;
        try
        {
            var user = await UserRepository.GetByName(userName);
            if (user is null || !Passwords.Verify(user, model.CurrentPassword, out _))
            {
                errorMessage = "The current password is not correct.";
                return;
            }

            user.Password = Passwords.Hash(user, model.NewPassword);
            await UserRepository.Update(user);

            Logger.LogInformation("Password changed for user {User}", userName);
            successMessage = "Password changed. Use the new password next time you log in.";
            model.CurrentPassword = model.NewPassword = model.ConfirmPassword = string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Change password failed for user {User}", userName);
            errorMessage = "The password could not be changed because of an unexpected error.";
        }
        finally
        {
            isBusy = false;
        }
    }
}
