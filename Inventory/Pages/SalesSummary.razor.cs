using System.Globalization;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
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
    private ComponentTotalData SalesTotalData = new();

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
                            DiscountPercent = savedSummary?.DiscountPercent ?? 0,
                            Amount = savedSummary?.Amount ?? 0m,
                            AmountAfterDiscount = savedSummary?.AmountAfterDiscount ?? 0m
                        };
                    })
                    .ToList();

                for (int i = 0; i < salesSummaryEntityList.Count; i++)
                {
                    salesSummaryEntityList[i].SerialNumber = i + 1;
                    var (amount, afterDiscount) = ComputeLineAmounts(
                        salesSummaryEntityList[i].ProductRate, salesSummaryEntityList[i].Quantity, salesSummaryEntityList[i].DiscountPercent);
                    salesSummaryEntityList[i].Amount = amount;
                    salesSummaryEntityList[i].AmountAfterDiscount = afterDiscount;
                }

                SalesTotalData = new()
                {
                    DiscountAmount = Math.Round(salesEntity.DiscountAmount, 2),
                };

                GetTotalAmount();
            }
        }
        StateHasChanged();
    }

    /// <summary>The amount and the amount after discount of one summary line, both rounded to 2 decimals (same helpers as the edit handlers).</summary>
    public static (decimal Amount, decimal AmountAfterDiscount) ComputeLineAmounts(decimal rate, int quantity, int discountPercent)
    {
        var amount = LineAmounts.ComputeAmount(rate, quantity);
        return (amount, LineAmounts.ComputeAmountAfterDiscount(amount, discountPercent) ?? 0);
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
            salesEntity.DiscountAmount = SalesTotalData.DiscountAmount;
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
                if (await DialogService.ConfirmDeleteAsync())
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
        var line = salesSummaryEntityList[index];
        line.Amount = LineAmounts.ComputeAmount(line.ProductRate, line.Quantity);
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
            Snackbar.Add($"DiscountPercent must be a whole number from 0 to 100 (percent). Kept {salesSummaryEntityList[index].DiscountPercent}.", Severity.Warning);
            inputVersion++;
            return;
        }

        salesSummaryEntityList[index].DiscountPercent = value;
        AmountAfterDiscountChanged(index);
        GetTotalAmount();
    }

    public void AmountAfterDiscountChanged(int index)
    {
        var line = salesSummaryEntityList[index];
        line.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(line.Amount, line.DiscountPercent) ?? 0;
    }

    private void DiscountForTotalAmountChanged(decimal value)
    {
        SalesTotalData.DiscountAmount = Math.Round(value, 2);
        GetTotalAmount();
    }

    public void GetTotalAmount()
    {
        SalesTotalData.TotalQuantity = salesSummaryEntityList.Select(x => x.Quantity).Sum();
        var amount = salesSummaryEntityList.Select(v => v.AmountAfterDiscount).Sum();

        //Calculate total after discount
        SalesTotalData.TotalAmount = Math.Round(amount - SalesTotalData.DiscountAmount, 2);
    }
}
