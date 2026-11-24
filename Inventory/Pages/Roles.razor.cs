using Inventory.MudBlazorComponents;
using Inventory.Service;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Inventory.Pages;

public partial class Roles
{
    [Inject] private RoleAdmin Admin { get; set; } = default!;
    [Inject] private UserAdmin UserAdmin { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<RoleRow> rows = new();
    private bool loaded;
    private bool mayManage;

    private static readonly DialogOptions PopUp = new() { CloseButton = true, MaxWidth = MaxWidth.Medium, FullWidth = true };

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        mayManage = await UserAdmin.MayManageUsersAsync(state.User.Identity?.Name);
        if (mayManage)
        {
            rows = await Admin.ListAsync();
        }

        loaded = true;
    }

    private static string CanDoText(RoleRow role)
    {
        if (role.IsAdmin)
        {
            return "Everything (cannot be changed)";
        }

        var labels = Permissions.All.Where(p => role.Permissions.Contains(p.Key)).Select(p => p.Label).ToList();
        return labels.Count == 0 ? "Nothing yet" : string.Join(", ", labels);
    }

    private Task AddRole() => ShowAsync("Add role", null);

    private Task EditRole(RoleRow role) => ShowAsync($"Edit role: {role.Name}", role);

    private async Task ShowAsync(string title, RoleRow? role)
    {
        var parameters = new DialogParameters<RoleEditDialog> { { x => x.Role, role } };
        var dialog = await DialogService.ShowAsync<RoleEditDialog>(title, parameters, PopUp);
        var result = await dialog.Result;
        if (result is { Canceled: false } && result.Data is string message)
        {
            Snackbar.Add(message, Severity.Success);
            rows = await Admin.ListAsync();
        }
    }

    private async Task DeleteRole(RoleRow role)
    {
        var confirmed = await DialogService.ConfirmAsync("Delete role", $"Delete the role {role.Name}?", "Delete", Color.Error);
        if (!confirmed)
        {
            return;
        }

        var result = await Admin.DeleteAsync(role.Id);
        Snackbar.Add(result.Message, result.Ok ? Severity.Success : Severity.Warning);
        rows = await Admin.ListAsync();
    }
}
