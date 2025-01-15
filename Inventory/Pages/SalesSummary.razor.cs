using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages
{
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
                if (SalesId != null)
                {
                    try
                    {
                        salesEntity = await SaleRepository.GetById(Guid.Parse(SalesId));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Sales summary error: " + ex.Message);
                    }
                    if (salesEntity != null)
                    {
                        salesSummaryModelList = salesEntity.SalesVariants.GroupBy(p => p.Product).Select(p => new SalesSummaryModel()
                        {
                            Product = p.Key,
                            Quantity = p.Sum(p => p.Quantity.Value),
                            
                        }).ToList();
                        if (salesSummaryModelList.Count != 0)
                        {
                            salesSummaryEntityList = salesSummaryModelList.Select(p => new SalesSummaryEntity()
                            {
                                ProductEntityId = p.Product.Id,
                                ProductName = p.Product.Name,
                                ProductId = p.Product.ProductId,
                                Quantity = p.Quantity,
                                ProductRate = p.Product.Rate.Value
                            }).ToList();

                            for (int i = 0; i < salesSummaryEntityList.Count; i++)
                            {
                                salesSummaryEntityList[i].SerialNumber = i + 1;
                                if (salesSummaryEntityList[i].ProductRate == 0 || salesSummaryEntityList[0].Quantity == 0)
                                    salesSummaryEntityList[i].Amount = 0;
                                else
                                    salesSummaryEntityList[i].Amount = Math.Round(salesSummaryEntityList[i].ProductRate * salesSummaryEntityList[i].Quantity, 2);
                                salesSummaryEntityList[i].AmountAfterDiscount = salesSummaryEntityList[i].Amount;
                            }
                            GetTotalAmount();
                           
                        }
                    }
                }
                StateHasChanged();
            }
          
        }

        public async Task AddSales()
        {
            if (salesEntity != null)
            {
                try
                {
                    salesEntity.SalesSummaries = salesSummaryEntityList;
                    salesEntity.TotalQuantity = SalesTotalData.TotalQuantity;
                    salesEntity.Discoint = SalesTotalData.Discount;
                    salesEntity.TotalAmountProduct = SalesTotalData.TotalAmount;
                    await SaleRepository.Update(salesEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Sales summary add: " + ex.Message);
                }
                navManager.NavigateTo("/sales");
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

        public void RateChanged(ChangeEventArgs arg, Guid productId)
        {
            var index = salesSummaryEntityList.FindIndex(n => n.ProductEntityId == productId);
            var value = decimal.Parse(arg.Value.ToString());
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
            var value = int.Parse(arg.Value.ToString());
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
            SalesTotalData.TotalAmount = amount - SalesTotalData.Discount;
        }
    }
}
