using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text.Json;

namespace Inventory.Pages
{
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

        private bool isVisibleCustomerPopup = false;
        private CustomerEntity customer = new();
        private CustomerModel customerModel;
        private SalesOrderModel salesOrderModel = new();
        private SalesOrderEntity salesOrderEntity = new();
        private ComponentTotalData SalesOrderTotalData = new(); 
        private List<CustomerModel> customers = new();
        private List<CustomerModel> customersAfterSearch = new();
        private bool IsDisabled { get; set; }

        protected async override Task OnInitializedAsync()
        {

            await GetCustomersAsync();
            if (SalesOrderId != null)
            {
                salesOrderEntity = await SalesOrderRepository.GetById(Guid.Parse(SalesOrderId));
                if (salesOrderEntity != null)
                {
                    salesOrderModel.VoucherId = salesOrderEntity.VoucherId;
                    salesOrderModel.OrderStatus = salesOrderEntity.OrderStatus;
                    salesOrderModel.CustomerId = salesOrderEntity.Customer.CustomerId;
                    salesOrderModel.CustomerName = salesOrderEntity.Customer.Name;
                    salesOrderModel.TotalAmountProduct = salesOrderEntity.TotalAmountProduct;
                    salesOrderModel.Date = salesOrderEntity.Date;
                    salesOrderModel.DueDate = salesOrderEntity.DueDate;

                    customer = salesOrderEntity.Customer;

                    customerModel = Mapper.Map<CustomerModel>(customer);


                    IsDisabled = false;
                    GetTotalAmount();

                    if (salesOrderEntity.TotalAmountProduct > 0)
                        SalesOrderTotalData.Discount = salesOrderEntity.SalesOrderVariants.Select(v => v.AmountAfterDiscount).Sum().Value - salesOrderEntity.TotalAmountProduct.Value;
                }
            }
            else
                IsDisabled = true;
        }

        public async Task AddOrder()
        {
            if (salesOrderModel != null)
            {
                try
                {
                    var voucherIdDb = await SalesOrderRepository.GetLastVoucherIdByDate(salesOrderModel.Date);
                    if (voucherIdDb == 0)
                        salesOrderModel.VoucherId = 1;
                    else
                        salesOrderModel.VoucherId = voucherIdDb + 1;

                    salesOrderEntity = new()
                    {
                        Date = salesOrderModel.Date,
                        DueDate = salesOrderModel.DueDate,
                        Customer = customer,
                        VoucherId = salesOrderModel.VoucherId,
                        OrderStatus = salesOrderModel.OrderStatus,
                    };
                    var id = await SalesOrderRepository.Create(salesOrderEntity);
                    SalesOrderId = id.ToString();
                    salesOrderModel.Id = id;
                    salesOrderEntity = await SalesOrderRepository.GetById(id);
                    IsDisabled = false;
                }
                catch (Exception ex)
                {
                    Snackbar.Add("Something went wrong", Severity.Warning);
                }
            }
        }

        public async Task EditOrder()
        {
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

                    if (salesOrderEntity.Customer.Id != customer.Id)
                        salesOrderEntity.Customer = customer;

                    salesOrderEntity.Date = salesOrderModel.Date;
                    salesOrderEntity.DueDate = salesOrderModel.DueDate;
                    salesOrderEntity.OrderStatus = salesOrderModel.OrderStatus;

                    await SalesOrderRepository.Update(salesOrderEntity);
                    Snackbar.Add("Updated successfully", Severity.Success);
                }
                catch (Exception ex)
                {
                    Snackbar.Add("Something went wrong", Severity.Warning);
                }
            }
        }

        public async Task DeleteOrder()
        {
            if (salesOrderEntity != null)
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
                        await SalesOrderRepository.Delete(salesOrderEntity);
                    }
                }
                catch (Exception ex)
                {
                    Snackbar.Add("Something went wrong", Severity.Warning);
                }
                navManager.NavigateTo("/salesorders");
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
                Snackbar.Add("Something went wrong", Severity.Warning);
            }
        }

        public async Task GetCustomerFromPopup()
        {
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Large,
                CloseOnEscapeKey = true,
                CloseButton = true,
                Position = DialogPosition.Center
            };

            var dialog = await DialogService.ShowAsync<CustomerDialog>("Customer Dialog", options);
            var result = await dialog.Result;


            if (!result.Canceled)
            {
                var customerFromPopup = (CustomerModel)result.Data;
                OnCustomerSelected(customerFromPopup);
            }
        }

        private void DiscountChanged(decimal value)
        {
            SalesOrderTotalData.Discount = value;
            GetTotalAmount();
        }

        public void GetTotalAmount()
        {
            SalesOrderTotalData.TotalQuantity = salesOrderEntity.SalesOrderVariants.Select(x => x.Quantity).Sum().Value;
            var amount = salesOrderEntity.SalesOrderVariants.Select(v => v.AmountAfterDiscount).Sum().Value;
            SalesOrderTotalData.TotalAmount = amount - SalesOrderTotalData.Discount;
        }


        private async Task GetCustomersAsync()
        {
            try
            {
                var customerDb = await CustomerRepository.GetAll();
                if (customerDb.Count != 0)
                {
                    customers = customerDb.Select(s => Mapper.Map<CustomerModel>(s)).ToList();
                    customersAfterSearch = customers;
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Snackbar.Add("Something went wrong", Severity.Warning);
            }
        }

        public void ChangeStateSalesOrderVariant(bool change)
        {
            if (change)
                GetTotalAmount();
        }

        //Customer selection
        private async Task<IEnumerable<CustomerModel>> SearchCustomerEntities(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value))
                return customersAfterSearch.ToList();
            return customers.Where(n => n.Name.ToLower().Contains(value))
                         .ToList() ?? null;
        }
        private async Task OnCustomerSelected(CustomerModel? _customer)
        {
            if (_customer != null)
            {
                customerModel = _customer;

                customer = await CustomerRepository.GetById(_customer.Id);

                salesOrderModel.CustomerId = _customer.CustomerId;
                salesOrderModel.CustomerName = _customer.Name;
            }
            else
            {
                customerModel = null;
                customer = new();
                // Clear the fields if no product is selected
                salesOrderModel.CustomerId = "";
                salesOrderModel.CustomerName = "";
            }
        }
    }
}
