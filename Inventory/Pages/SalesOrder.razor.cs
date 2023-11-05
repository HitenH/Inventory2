using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class SalesOrder
    {
        [Parameter] public string SalesOrderId { get; set; }
        [Inject] private ISalesOrderRepository SalesOrderRepository { get; set; }
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }

        private bool isVisibleCustomerPopup = false;
        private CustomerEntity customer = new();
        private SalesOrderModel salesOrderModel = new();
        private SalesOrderEntity salesOrderEntity = new();
        private ComponentTotalData SalesOrderTotalData = new();
        private bool IsDisabled { get; set; }

        protected async override Task OnInitializedAsync()
        {
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
                    IsDisabled = false;
                    GetTotalAmount();
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
                    var voucherIdDb = await SalesOrderRepository.GetLastVoucherId();
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
                    Logger.LogError("Add new order error: " + ex.Message);
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
                        isVoucherExist = SalesOrderRepository.IsVoucherExist(salesOrderModel.VoucherId);

                    if (!isVoucherExist)
                    {
                        if (salesOrderEntity.VoucherId != salesOrderModel.VoucherId)
                            salesOrderEntity.VoucherId = salesOrderModel.VoucherId;
                        if (salesOrderEntity.Customer.Id != customer.Id)
                            salesOrderEntity.Customer = customer;

                        salesOrderEntity.Date = salesOrderModel.Date;
                        salesOrderEntity.DueDate = salesOrderModel.DueDate;
                        salesOrderEntity.OrderStatus = salesOrderModel.OrderStatus;

                        var id = await SalesOrderRepository.Update(salesOrderEntity);
                    }
                    else
                        salesOrderModel.VoucherId = salesOrderEntity.VoucherId;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update order: " + ex.Message);
                }
            }
        }

        public async Task DeleteOrder()
        {
            if (salesOrderEntity != null)
            {
                try
                {
                    await SalesOrderRepository.Delete(salesOrderEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete order: " + ex.Message);
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
                Logger.LogError("Add total amount: " + ex.Message);
            }
        }

        public void OpenCustomerPopup()
        {
            isVisibleCustomerPopup = true;
        }
        public void CloseCustomerPopup(bool state)
        {
            isVisibleCustomerPopup = state;
        }

        public async void GetCustomerFromPopup(CustomerModel customerFromPopup)
        {
            if (customerFromPopup != null)
            {
                salesOrderModel.CustomerId = customerFromPopup.CustomerId;
                salesOrderModel.CustomerName = customerFromPopup.Name;
                customer = await CustomerRepository.GetById(customerFromPopup.Id);
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

        public void ChangeStateSalesOrderVariant(bool change)
        {
            if (change)
                GetTotalAmount();
        }
    }
}
