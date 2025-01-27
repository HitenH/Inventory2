using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages;

[Authorize]
public partial class SalesOrders
{
    [Inject] public ISalesOrderRepository SalesOrderRepository { get; set; }
    [Inject] private ICustomerRepository CustomerRepository { get; set; }
    [Inject] public ILogger<SalesOrders> Logger { get; set; }
    [Inject] IDialogService DialogService { get; set; }
    [Inject] ISnackbar Snackbar { get; set; }

    private List<SalesOrderModel> salesOrders = new();
    //Get enums list to populate order status
    private List<OrderStatus> orderStatuses = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().ToList();
    private List<SalesOrderModel> salesOrdersAfterSearch = new();
    private List<SalesOrderEntity> salesOrdersDb = new();
    private HashSet<SalesOrderModel> selectedSalesOrders = new();
    private SalesOrderModel salesOrderModelBackup = new();
    private Snackbar snackbar;
    private bool isSortAscending = false;
    private bool isSelected = false;

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await GetOrders();
        }
    }

    public async Task GetOrders()
    {
        try
        {
            salesOrdersDb = await SalesOrderRepository.GetAll();
            salesOrders = salesOrdersDb.Select(o => new SalesOrderModel()
            {
                CustomerId = o.Customer.CustomerId,
                CustomerName = o.Customer.Name,
                Date = o.Date,
                DueDate = o.DueDate,
                Id = o.Id,
                VoucherId = o.VoucherId,
                OrderStatus = o.OrderStatus,
                TotalAmountProduct = o.TotalAmountProduct
            }).ToList();
            salesOrdersAfterSearch = [.. salesOrders.OrderByDescending(o => o.Date)];
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError("SalesOrders error: " + ex.Message);
        }
    }

    public void SearchItem(ChangeEventArgs e)
    {
        var search = e.Value.ToString().ToLower();
        salesOrdersAfterSearch = salesOrders.Where(n => n.CustomerName.ToLower().Contains(search)
                                           || n.VoucherId.ToString().Contains(search)
                                           || n.Date.ToString().Contains(search)
                                           || n.DueDate.ToString().Contains(search)
                                           || n.OrderStatus.ToString().ToLower().Contains(search)).ToList();
    }
    public async Task DeleteSelectedOrders()
    {
        if (salesOrdersAfterSearch.Count > 0)
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
                    salesOrdersDb = salesOrdersDb.Where(q => selectedSalesOrders.Any(p => p.Id == q.Id)).ToList();
                    await SalesOrderRepository.DeleteRange(salesOrdersDb);
                    //Snackbar to inform user of process
                    snackbar = Snackbar.Add($"Deleted records successfully.", Severity.Success);

                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete selected orders error:" + ex.Message);
            }
        }
    }

    public async Task DeleteCompletedOrders()
    {
        if (salesOrdersAfterSearch.Count > 0)
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
                    salesOrdersDb = salesOrdersDb.Where(o => salesOrdersAfterSearch.Where(or => or.OrderStatus == OrderStatus.Completed).Any(p => p.Id == o.Id)).ToList();
                    await SalesOrderRepository.DeleteRange(salesOrdersDb);
                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete completed orders error:" + ex.Message);
            }
        }
    }

    //Edit row methods
    private void BackupItem(object item)
    {
        salesOrderModelBackup = (SalesOrderModel)item;
    }
    private void ResetItemToOriginalValues(object item)
    {
        var index = salesOrdersAfterSearch.IndexOf((SalesOrderModel)item);
        salesOrdersAfterSearch[index] = salesOrderModelBackup;
    }
    public async void EditOrder(object e)
    {
        SalesOrderEntity salesOrderEntity = new();
        SalesOrderModel salesOrderModel = ((SalesOrderModel)e);

        if (salesOrderModel != null)
        {
            try
            {
                var isVoucherExist = false;
                if (salesOrderEntity.VoucherId != salesOrderModel.VoucherId)
                    isVoucherExist = SalesOrderRepository.IsVoucherExistByDate(salesOrderModel.VoucherId, salesOrderModel.Date);

                if (!isVoucherExist)
                    salesOrderEntity.VoucherId = salesOrderModel.VoucherId;
                else
                    salesOrderModel.VoucherId = salesOrderEntity.VoucherId;

                //salesOrderEntity.Customer = await CustomerRepository.GetByCustomId(salesOrderModel.CustomerId);
                salesOrderEntity.Date = salesOrderModel.Date;
                salesOrderEntity.DueDate = salesOrderModel.DueDate;
                salesOrderEntity.OrderStatus = salesOrderModel.OrderStatus;

                await SalesOrderRepository.Update(salesOrderEntity);
                await GetOrders();
                snackbar = Snackbar.Add($"Updated {salesOrderModel.Id} successfully.", Severity.Success);
            }
            catch (Exception ex)
            {
                snackbar = Snackbar.Add($"Something went wrong: {ex.Message}", Severity.Warning);
            }
        }
    }
}
