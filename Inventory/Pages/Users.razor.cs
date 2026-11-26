using Inventory.MudBlazorComponents;
using Inventory.Service;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Inventory.Pages;

public partial class Users
{
    [Inject] private UserAdmin Admin { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private PlaceService PlaceService { get; set; } = default!;

    private List<UserRow> rows = new();
    private List<RoleChoice> roles = new();
    private List<PlaceItem> places = new();
    private string? currentUser;
    private bool loaded;
    private bool mayManage;

    private static readonly DialogOptions PopUp = new() { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        currentUser = state.User.Identity?.Name;
        mayManage = await Admin.MayManageUsersAsync(currentUser);
        if (mayManage)
        {
            await ReloadAsync();
        }

        loaded = true;
    }

    private async Task ReloadAsync()
    {
        rows = await Admin.ListAsync();
        roles = await Admin.ListRolesAsync();
        places = await PlaceService.AllAsync();
    }

    private static string RolesText(UserRow user)
        => user.Roles.Count == 0
            ? "-"
            : string.Join(", ", user.Roles.Select(r => r.ExpiresAt is { } end ? $"{r.RoleName} (to {end:d MMM yyyy})" : r.RoleName));

    /// <summary>Stage 3, batch 3-3: "All places", or the names of the user's places.</summary>
    private string PlacesText(UserRow user) =>
        user.PlaceIds.Count == 0 ? "All places" : string.Join(", ", places.Where(p => user.PlaceIds.Contains(p.Id)).Select(p => p.Name));

    private async Task EditPlaces(UserRow user)
    {
        var parameters = new DialogParameters<UserPlacesDialog>
        {
            { x => x.UserId, user.Id }, { x => x.Places, places }, { x => x.Current, user.PlaceIds }
        };
        await ShowAsync<UserPlacesDialog>($"Places for {user.Name}", parameters);
    }

    private async Task AddUser()
    {
        var parameters = new DialogParameters<AddUserDialog> { { x => x.Roles, roles } };
        await ShowAsync<AddUserDialog>("Add user", parameters);
    }

    private async Task EditRoles(UserRow user)
    {
        var parameters = new DialogParameters<UserRolesDialog>
        {
            { x => x.UserId, user.Id }, { x => x.Roles, roles }, { x => x.Current, user.Roles }
        };
        await ShowAsync<UserRolesDialog>($"Roles for {user.Name}", parameters);
    }

    private async Task ResetPassword(UserRow user)
    {
        var parameters = new DialogParameters<UserPasswordDialog> { { x => x.UserId, user.Id } };
        await ShowAsync<UserPasswordDialog>($"New password for {user.Name}", parameters);
    }

    private async Task ShowAsync<TDialog>(string title, DialogParameters<TDialog> parameters) where TDialog : ComponentBase
    {
        var dialog = await DialogService.ShowAsync<TDialog>(title, parameters, PopUp);
        var result = await dialog.Result;
        if (result is { Canceled: false } && result.Data is string message)
        {
            Snackbar.Add(message, Severity.Success);
            await ReloadAsync();
        }
    }

    private async Task Disable(UserRow user)
    {
        var confirmed = await DialogService.ConfirmAsync("Disable user",
            $"Disable {user.Name}? They will not be able to log in. Their history stays, and you can enable them again.",
            "Disable", Color.Error);
        if (!confirmed)
        {
            return;
        }

        await ShowResultAsync(await Admin.DisableAsync(user.Id, currentUser));
    }

    private async Task Enable(UserRow user) => await ShowResultAsync(await Admin.EnableAsync(user.Id));

    private async Task ShowResultAsync(UserAdminResult result)
    {
        Snackbar.Add(result.Message, result.Ok ? Severity.Success : Severity.Warning);
        await ReloadAsync();
    }
}
