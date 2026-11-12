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
    [Inject] public ILogger<SalesOrders> Logger { get; set; }
    [Inject] IDialogService DialogService { get; set; }
    [Inject] ISnackbar Snackbar { get; set; }

    private List<SalesOrderModel> salesOrders = new();
    private List<SalesOrderModel> salesOrdersAfterSearch = new();
    private List<SalesOrderEntity> salesOrdersDb = new();
    private HashSet<SalesOrderModel> selectedSalesOrders = new();

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
            Logger.LogError(ex, "SalesOrders error");
        }
    }

    // Batch 14: the list shows one page at a time (see ListPaging).
    private MudBlazor.MudTable<SalesOrderModel>? listTable;

    public void SearchItem(ChangeEventArgs e)
    {
        listTable?.NavigateTo(0);   // a new search starts on page 1 (batch 14)
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
                if (await DialogService.ConfirmDeleteAsync(selectedSalesOrders.Count))
                {
                    salesOrdersDb = salesOrdersDb.Where(q => selectedSalesOrders.Any(p => p.Id == q.Id)).ToList();
                    await SalesOrderRepository.DeleteRange(salesOrdersDb);
                    //Snackbar to inform user of process
                    Snackbar.Add($"Deleted records successfully.", Severity.Success);

                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete selected orders error");
            }
        }
    }

    public async Task DeleteCompletedOrders()
    {
        if (salesOrdersAfterSearch.Count > 0)
        {
            try
            {
                if (await DialogService.ConfirmDeleteAsync(salesOrdersAfterSearch.Count(o => o.OrderStatus == OrderStatus.Completed)))
                {
                    salesOrdersDb = salesOrdersDb.Where(o => salesOrdersAfterSearch.Where(or => or.OrderStatus == OrderStatus.Completed).Any(p => p.Id == o.Id)).ToList();
                    await SalesOrderRepository.DeleteRange(salesOrdersDb);
                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete completed orders error");
            }
        }
    }
}
