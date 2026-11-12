using System.Globalization;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages;

[Authorize]
public partial class SalesSummary
{
    [Parameter] public string SalesId { get; set; }
    [Inject] public ISaleRepository SaleRepository { get; set; }
    [Inject] private NavigationManager navManager { get; set; }
    [Inject] private IDialogService DialogService { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private ILogger<SalesSummary> Logger { get; set; }

    private SalesEntity salesEntity = new();
    public List<SalesSummaryEntity> salesSummaryEntityList = new();
    public List<SalesSummaryModel> salesSummaryModelList = new();
    private ComponentTotalData SalesTotalData = new();

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
        }
    }

    protected override async Task OnInitializedAsync()
    {

        if (SalesId != null)
        {
            try
            {
                salesEntity = await SaleRepository.GetById(Guid.Parse(SalesId));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Sales summary error");
            }
            if (salesEntity.Id != Guid.Empty && salesEntity.SalesVariants is { Count: > 0 })
            {
                var summaryDict = salesEntity.SalesSummaries?.ToDictionary(s => s.ProductEntityId)
                    ?? new Dictionary<Guid, SalesSummaryEntity>();

                salesSummaryEntityList = salesEntity.SalesVariants
                    .GroupBy(v => v.ProductEntityId)
                    .Select(group =>
                    {
                        var first = group.First();
                        var product = first.Product;
                        summaryDict.TryGetValue(group.Key, out var savedSummary);

                        return new SalesSummaryEntity
                        {
                            ProductEntityId = group.Key,
                            ProductId = product?.ProductId ?? savedSummary?.ProductId ?? "N/A",
                            ProductName = product?.Name ?? savedSummary?.ProductName ?? "Unknown",
                            Quantity = group.Sum(v => v.Quantity ?? 0),
                            ProductRate = savedSummary?.ProductRate ?? product?.Rate ?? 0m,
                            Discount = savedSummary?.Discount ?? 0,
                            Amount = savedSummary?.Amount ?? 0m,
                            AmountAfterDiscount = savedSummary?.AmountAfterDiscount ?? 0m
                        };
                    })
                    .ToList();

                for (int i = 0; i < salesSummaryEntityList.Count; i++)
                {
                    salesSummaryEntityList[i].SerialNumber = i + 1;
                    if (salesSummaryEntityList[i].ProductRate == 0 || salesSummaryEntityList[i].Quantity == 0)
                    {
                        salesSummaryEntityList[i].Amount = 0;
                    }
                    else
                    {
                        salesSummaryEntityList[i].Amount = Math.Round(
                            salesSummaryEntityList[i].ProductRate * salesSummaryEntityList[i].Quantity, 2);
                    }

                    salesSummaryEntityList[i].AmountAfterDiscount = salesSummaryEntityList[i].Amount
                        * (1 - (salesSummaryEntityList[i].Discount / 100m));
                }

                SalesTotalData = new()
                {
                    Discount = salesEntity.Discount,
                };

                GetTotalAmount();
            }
        }
        StateHasChanged();
    }

    public async Task AddSales()
    {
        if (salesEntity != null && await SaveSummaryAsync())
        {
            Snackbar.Add("Sale made successfully", Severity.Success);
            navManager.NavigateTo("/sales");
        }
    }

    // "Back To Scanning Area" saves the rates, discounts and remarks first; it used to leave without saving, so they were lost.
    public async Task BackToScanning()
    {
        if (salesEntity != null && salesEntity.Id != Guid.Empty && await SaveSummaryAsync())
        {
            navManager.NavigateTo($"/sale/{salesEntity.Id}");
        }
    }

    /// <summary>Saves the summary; on failure shows the reason and stays on the page so nothing typed is lost.</summary>
    private async Task<bool> SaveSummaryAsync()
    {
        try
        {
            salesEntity.SalesSummaries = salesSummaryEntityList;
            salesEntity.TotalQuantity = SalesTotalData.TotalQuantity;
            salesEntity.Discount = SalesTotalData.Discount;
            salesEntity.TotalAmountProduct = SalesTotalData.TotalAmount;
            // null = leave the scanned items as they are (only the summary is saved here)
            salesEntity.SalesVariants = null;
            await SaleRepository.Update(salesEntity);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Save sale summary failed");
            Snackbar.Add($"Sale failed: {ex.Message}", Severity.Error);
            return false;
        }
    }

    public async Task DeleteSales()
    {
        if (salesEntity != null)
        {
            try
            {
                var parameters = new DialogParameters<ConfirmationDialog>
                {
                    { x => x.ContentText, "Do you really want to delete these records? This process cannot be undone." },
                    { x => x.ButtonText, "Delete" },
                    { x => x.Color, Color.Error }
                };

                var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };

                var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete", parameters, options);
                var result = await dialog.Result;
                if (!result.Canceled)
                {
                    await SaleRepository.Delete(salesEntity);
                    Snackbar.Add("Deleted sale successfully", Severity.Success);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error deleting sale: {ex.Message}", Severity.Error);
            }
            navManager.NavigateTo("/sales");
        }
    }

    // Bumped when a typed value is rejected, so the input boxes are redrawn with the previous value.
    private int inputVersion;

    public void RateChanged(ChangeEventArgs arg, Guid productId)
    {
        var index = salesSummaryEntityList.FindIndex(n => n.ProductEntityId == productId);
        if (index < 0)
            return;

        // Empty box = 0. The browser sends numbers with a dot, whatever the PC's regional settings.
        var text = arg.Value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
            text = "0";

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            Snackbar.Add($"Rate must be a number of 0 or more. Kept {salesSummaryEntityList[index].ProductRate}.", Severity.Warning);
            inputVersion++;
            return;
        }

        salesSummaryEntityList[index].ProductRate = value;
        AmountChanged(index);
        GetTotalAmount();
    }

    public void AmountChanged(int index)
    {
        if (salesSummaryEntityList[index].ProductRate == 0 || salesSummaryEntityList[index].Quantity == 0)
            salesSummaryEntityList[index].Amount = 0;
        else
            salesSummaryEntityList[index].Amount = Math.Round(salesSummaryEntityList[index].ProductRate * salesSummaryEntityList[index].Quantity, 2);
        AmountAfterDiscountChanged(index);
    }

    public void DiscountChanged(ChangeEventArgs arg, Guid productId)
    {
        var index = salesSummaryEntityList.FindIndex(n => n.ProductEntityId == productId);
        if (index < 0)
            return;

        // Discount is a whole-number percentage. Empty box = 0.
        var text = arg.Value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
            text = "0";

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 100)
        {
            Snackbar.Add($"Discount must be a whole number from 0 to 100 (percent). Kept {salesSummaryEntityList[index].Discount}.", Severity.Warning);
            inputVersion++;
            return;
        }

        salesSummaryEntityList[index].Discount = value;
        AmountAfterDiscountChanged(index);
        GetTotalAmount();
    }

    public void AmountAfterDiscountChanged(int index)
    {
        if (salesSummaryEntityList[index].Amount != 0 && salesSummaryEntityList[index].Discount != 0)
        {
            var amount = salesSummaryEntityList[index].Amount - (salesSummaryEntityList[index].Amount * ((decimal)salesSummaryEntityList[index].Discount / 100));
            salesSummaryEntityList[index].AmountAfterDiscount = Math.Round(amount, 2);
        }
        else
        {
            salesSummaryEntityList[index].AmountAfterDiscount = Math.Round(salesSummaryEntityList[index].Amount, 2);
        }
    }

    private void DiscountForTotalAmountChanged(decimal value)
    {
        SalesTotalData.Discount = value;
        GetTotalAmount();
    }

    public void GetTotalAmount()
    {
        SalesTotalData.TotalQuantity = salesSummaryEntityList.Select(x => x.Quantity).Sum();
        var amount = salesSummaryEntityList.Select(v => v.AmountAfterDiscount).Sum();

        //Calculate total after discount
        SalesTotalData.TotalAmount = amount - SalesTotalData.Discount;
    }
}
