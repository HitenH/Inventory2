using MudBlazor;

namespace Inventory.MudBlazorComponents;

/// <summary>
/// The one place the "Are you sure?" box is shown (batch 12; was copied into 12 screens).
/// </summary>
public static class DialogServiceExtensions
{
    public const string DeleteWarning = "Do you really want to delete these records? This process cannot be undone.";

    /// <summary>Shows the confirmation box. True only when the user clicks the button (not Cancel or X).</summary>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogService, string title, string text, string buttonText, Color color)
    {
        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, text },
            { x => x.ButtonText, buttonText },
            { x => x.Color, color }
        };
        var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };
        var dialog = await dialogService.ShowAsync<ConfirmationDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>Error box with a short message and a "Show Full" button for the details (batch 13).</summary>
    public static async Task ShowErrorDetailsAsync(this IDialogService dialogService, string title, string messageShort, string messageLong)
    {
        var parameters = new DialogParameters<ErrorDetailsDialog>
        {
            { x => x.MessageShort, messageShort },
            { x => x.MessageLong, messageLong }
        };
        var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await dialogService.ShowAsync<ErrorDetailsDialog>(title, parameters, options);
        await dialog.Result;
    }

    /// <summary>The standard red "Delete" confirmation used before every delete.</summary>
    public static Task<bool> ConfirmDeleteAsync(this IDialogService dialogService)
        => dialogService.ConfirmAsync("Delete", DeleteWarning, "Delete", Color.Error);
}
