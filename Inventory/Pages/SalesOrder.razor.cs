using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages;

[Authorize]
public partial class SalesOrder
{
    [Parameter] public string SalesOrderId { get; set; }
    [Inject] private ISalesOrderRepository SalesOrderRepository { get; set; }
    [Inject] private ICustomerRepository CustomerRepository { get; set; }
    [Inject] private ILogger<SalesOrder> Logger { get; set; }
    [Inject] private NavigationManager navManager { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private IDialogService DialogService { get; set; }
    [Inject] private IMapper Mapper { get; set; }

    private CustomerEntity customer = new();
    private CustomerModel? customerModel;
    private SalesOrderModel salesOrderModel = new();
    private SalesOrderEntity salesOrderEntity = new();
    private ComponentTotalData SalesOrderTotalData = new();
    private bool IsDisabled { get; set; }

    protected async override Task OnInitializedAsync()
    {
        if (SalesOrderId != null)
        {
            await ReloadSalesOrderAsync();
            IsDisabled = salesOrderEntity.Id == Guid.Empty;
        }
        else
        {
            IsDisabled = true;
        }
    }

    private async Task ReloadSalesOrderAsync()
    {
        salesOrderEntity = await SalesOrderRepository.GetById(Guid.Parse(SalesOrderId!));
        if (salesOrderEntity.Id == Guid.Empty)
        {
            return;
        }

        salesOrderModel.VoucherId = salesOrderEntity.VoucherId;
        salesOrderModel.OrderStatus = salesOrderEntity.OrderStatus;
        salesOrderModel.CustomerId = salesOrderEntity.Customer?.CustomerId ?? "";
        salesOrderModel.CustomerName = salesOrderEntity.Customer?.Name ?? "";
        salesOrderModel.TotalAmountProduct = salesOrderEntity.TotalAmountProduct;
        salesOrderModel.Date = salesOrderEntity.Date;
        salesOrderModel.DueDate = salesOrderEntity.DueDate;
        salesOrderModel.Id = salesOrderEntity.Id;

        customer = salesOrderEntity.Customer ?? new CustomerEntity();
        customerModel = Mapper.Map<CustomerModel>(customer);

        GetTotalAmount();
        if (salesOrderEntity.TotalAmountProduct > 0)
        {
            var lineTotal = (salesOrderEntity.SalesOrderVariants ?? new List<SalesOrderVariantEntity>())
                .Select(v => v.AmountAfterDiscount).Sum() ?? 0;
            SalesOrderTotalData.Discount = lineTotal - salesOrderEntity.TotalAmountProduct.Value;
        }
    }

    public async Task AddOrder()
    {
        if (customer.Id == Guid.Empty)
        {
            Snackbar.Add("Please select a customer.", Severity.Warning);
            return;
        }

        try
        {
            var voucherIdDb = await SalesOrderRepository.GetLastVoucherIdByDate(salesOrderModel.Date);
            salesOrderModel.VoucherId = voucherIdDb == 0 ? 1 : voucherIdDb + 1;

            salesOrderEntity = new()
            {
                Date = salesOrderModel.Date,
                DueDate = salesOrderModel.DueDate,
                CustomerEntityId = customer.Id,
                VoucherId = salesOrderModel.VoucherId,
                OrderStatus = salesOrderModel.OrderStatus,
            };
            var id = await SalesOrderRepository.Create(salesOrderEntity);
            SalesOrderId = id.ToString();
            salesOrderModel.Id = id;
            await ReloadSalesOrderAsync();
            IsDisabled = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Create sales order failed");
            Snackbar.Add($"Saving error: {ex.Message}", Severity.Error);
        }
    }

    public async Task EditOrder()
    {
        if (salesOrderEntity.Id == Guid.Empty || customer.Id == Guid.Empty)
        {
            Snackbar.Add("Please select a customer.", Severity.Warning);
            return;
        }

        try
        {
            var isVoucherExist = false;
            if (salesOrderEntity.VoucherId != salesOrderModel.VoucherId)
            {
                isVoucherExist = await SalesOrderRepository.IsVoucherExistByDate(salesOrderModel.VoucherId, salesOrderModel.Date);
            }

            if (!isVoucherExist)
            {
                salesOrderEntity.VoucherId = salesOrderModel.VoucherId;
            }
            else
            {
                salesOrderModel.VoucherId = salesOrderEntity.VoucherId;
            }

            salesOrderEntity.CustomerEntityId = customer.Id;
            salesOrderEntity.Date = salesOrderModel.Date;
            salesOrderEntity.DueDate = salesOrderModel.DueDate;
            salesOrderEntity.OrderStatus = salesOrderModel.OrderStatus;

            await SalesOrderRepository.Update(salesOrderEntity);
            await ReloadSalesOrderAsync();
            Snackbar.Add("Updated successfully", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Update sales order failed");
            Snackbar.Add($"Update error: {ex.Message}", Severity.Error);
        }
    }

    public async Task DeleteOrder()
    {
        if (salesOrderEntity.Id == Guid.Empty)
        {
            return;
        }

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
                await SalesOrderRepository.Delete(salesOrderEntity);
                navManager.NavigateTo("/salesorders");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Delete sales order failed");
            Snackbar.Add("Something went wrong", Severity.Warning);
        }
    }

    public async Task AddTotalAmount()
    {
        try
        {
            salesOrderEntity.TotalAmountProduct = SalesOrderTotalData.TotalAmount;
            await SalesOrderRepository.Update(salesOrderEntity);
            navManager.NavigateTo("/salesorders");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Save sales order totals failed");
            Snackbar.Add($"Could not save totals: {ex.Message}", Severity.Error);
        }
    }

    private void DiscountChanged(decimal value)
    {
        SalesOrderTotalData.Discount = value;
        GetTotalAmount();
    }

    public void GetTotalAmount()
    {
        var lines = salesOrderEntity.SalesOrderVariants ?? new List<SalesOrderVariantEntity>();
        SalesOrderTotalData.TotalQuantity = lines.Select(x => x.Quantity).Sum() ?? 0;
        var amount = lines.Select(v => v.AmountAfterDiscount).Sum() ?? 0;
        SalesOrderTotalData.TotalAmount = amount - SalesOrderTotalData.Discount;
    }

    public async Task ChangeStateSalesOrderVariant(bool change)
    {
        if (change && SalesOrderId != null)
        {
            await ReloadSalesOrderAsync();
            StateHasChanged();
        }
    }

    private async Task OnCustomerSelected(CustomerModel? _customer)
    {
        if (_customer != null)
        {
            customerModel = _customer;
            customer = await CustomerRepository.GetById(_customer.Id);
            salesOrderModel.CustomerId = _customer.CustomerId ?? "";
            salesOrderModel.CustomerName = _customer.Name ?? "";

            if (salesOrderEntity.Id != Guid.Empty)
            {
                salesOrderEntity.CustomerEntityId = customer.Id;
            }
        }
        else
        {
            customerModel = null;
            customer = new();
            salesOrderModel.CustomerId = "";
            salesOrderModel.CustomerName = "";
        }
    }
}
