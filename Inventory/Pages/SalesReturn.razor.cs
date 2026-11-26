using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Inventory.Service;
using Inventory.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Inventory.Pages;

/// <summary>Stage 2, batch 2-4: one sales return. Saves go through <see cref="StockChangeService"/>.</summary>
[Authorize]
public partial class SalesReturn
{
    [Parameter] public string? ReturnId { get; set; }
    [CascadingParameter] private UserAccess? Access { get; set; }

    [Inject] private StockChangeService StockChanges { get; set; } = default!;
    [Inject] private ICustomerRepository CustomerRepository { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<SalesReturn> Logger { get; set; } = default!;

    /// <summary>What the top of the screen and the remarks box hold.</summary>
    private sealed class Header
    {
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public string? Remarks { get; set; }
    }

    private readonly Header header = new();
    private CustomerModel? selectedCustomer;
    private StockChangeEntity? salesReturn;
    private TransactionLines lines = default!;

    private int TotalPieces => salesReturn?.Lines.Where(l => l.From == StockState.Outside).Sum(l => l.Quantity) ?? 0;

    /// <summary>Hiten, 26 Nov 2026: goods given to the customer in exchange (they leave sellable stock).</summary>
    private List<StockChangeLineEntity> Given => salesReturn?.Lines.Where(l => l.To == StockState.Outside).OrderBy(l => l.SerialNumber).ToList() ?? [];

    private int GivenPieces => Given.Sum(l => l.Quantity);

    protected override async Task OnInitializedAsync()
    {
        if (ReturnId != null)
        {
            await ReloadAsync();
        }
    }

    private async Task ReloadAsync()
    {
        if (!Guid.TryParse(ReturnId, out var id) || await StockChanges.GetSalesReturnAsync(id) is not { } loaded)
        {
            Snackbar.Add("This sales return was not found.", Severity.Warning);
            Nav.NavigateTo("/salesreturns");
            return;
        }

        salesReturn = loaded;
        lines = new SalesReturnLines(loaded, StockChanges);
        header.Date = loaded.Date;
        header.Remarks = loaded.Remarks;
        selectedCustomer = loaded.Customer?.ToModel();
    }

    // Enter on a chosen customer: a new return is saved at once (the cursor then goes to the line's product box).
    private async Task OnCustomerEnter(CustomerModel _)
    {
        if (ReturnId == null)
        {
            await SaveHeaderAsync();
        }
        else
        {
            await JS.FocusBoxAsync(".focus-line-product");
        }
    }

    private void OnCustomerSelected(CustomerModel? customer) => selectedCustomer = customer is { Id: var id } && id != Guid.Empty ? customer : null;

    private async Task SaveHeaderAsync()
    {
        if (selectedCustomer is null || selectedCustomer.Id == Guid.Empty)
        {
            Snackbar.Add("Please select a customer.", Severity.Warning);
            return;
        }

        try
        {
            if (ReturnId == null)
            {
                var created = await StockChanges.CreateSalesReturnAsync(selectedCustomer.Id, header.Date, header.Remarks);
                ReturnId = created.Id.ToString();
                // Like a new purchase, the screen stays where it is (a change of address would end "being created",
                // and the lines added now belong to creating it).
                await ReloadAsync();
                await JS.FocusBoxAsync(".focus-line-product");
            }
            else
            {
                await StockChanges.UpdateSalesReturnAsync(salesReturn!.Id, selectedCustomer.Id, header.Date, header.Remarks);
                await ReloadAsync();
                Snackbar.Add("Sales return updated.", Severity.Success);
            }
        }
        catch (StockChangeRefused ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Save sales return failed");
            Snackbar.Add($"Could not save the sales return: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>The Save button of the bottom bar: saves the remarks and goes back to the list (like "Save Total Amount" on a purchase).</summary>
    private async Task SaveAndCloseAsync()
    {
        if (salesReturn is null || selectedCustomer is null)
        {
            return;
        }

        try
        {
            await StockChanges.UpdateSalesReturnAsync(salesReturn.Id, selectedCustomer.Id, header.Date, header.Remarks);
            Nav.NavigateTo("/salesreturns");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Save sales return failed");
            Snackbar.Add($"Could not save the sales return: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (salesReturn is null || !await DialogService.ConfirmDeleteAsync())
        {
            return;
        }

        try
        {
            await StockChanges.DeleteSalesReturnAsync(salesReturn.Id);
            Snackbar.Add("Sales return deleted", Severity.Success);
            Nav.NavigateTo("/salesreturns");
        }
        catch (StockChangeRefused ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete sales return failed");
            Snackbar.Add("Could not delete the sales return.", Severity.Error);
        }
    }

    private async Task LinesChangedAsync(bool changed)
    {
        if (changed)
        {
            await ReloadAsync();
            StateHasChanged();
        }
    }

    private async Task RecordDamageAsync()
    {
        if (salesReturn is null)
        {
            return;
        }

        var title = $"Record damage – Sales return {salesReturn.VoucherId} of {salesReturn.Date:dd-MM-yyyy}, {salesReturn.Customer?.Name}";
        var saved = await RecordDamageDialog.ShowAsync(DialogService, DamageSource.SalesReturn, salesReturn.Id, title);
        if (saved is { Changed: true })
        {
            Snackbar.Add(saved.Text, Severity.Success);
        }

        await JS.FocusBoxAsync(".focus-line-product");
    }

    /// <summary>"Given in exchange": the Bulk Entry pop-up in its quantities-only form; all rows saved together.</summary>
    private async Task GiveInExchangeAsync()
    {
        if (salesReturn is null)
        {
            return;
        }

        var result = await BulkEntryDialog.ShowAsync(DialogService, BulkEntryKind.SalesOrder, quantitiesOnly: true,
            title: $"Given in exchange – Sales return {salesReturn.VoucherId}, {salesReturn.Customer?.Name}");
        if (result is null)
        {
            await JS.FocusBoxAsync(".focus-line-product");
            return;
        }

        try
        {
            await StockChanges.AddExchangeLinesAsync(salesReturn.Id, result.Rows.Select(r => (r.ProductEntityId, r.VariantEntityId, (int?)r.Quantity)).ToList());
            var pieces = result.Rows.Sum(r => r.Quantity);
            Snackbar.Add($"Given in exchange: {pieces} {(pieces == 1 ? "piece" : "pieces")}.", Severity.Success);
            await ReloadAsync();
        }
        catch (StockChangeRefused ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Given in exchange failed");
            Snackbar.Add("Could not save the goods given in exchange. Nothing was saved. " + (DbErrors.RuleMessage(ex) ?? ""), Severity.Error);
        }

        await JS.FocusBoxAsync(".focus-line-product");
    }

    private async Task RemoveGivenAsync(Guid lineId)
    {
        try
        {
            await StockChanges.DeleteSalesReturnLineAsync(lineId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Removing a line given in exchange failed");
            Snackbar.Add($"Could not remove the line: {ex.Message}", Severity.Error);
        }
    }
}
