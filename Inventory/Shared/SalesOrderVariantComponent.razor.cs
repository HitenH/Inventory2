using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Pages;
using Inventory.Service;
using Microsoft.AspNetCore.Components;

namespace Inventory.Shared
{
    public partial class SalesOrderVariantComponent
    {
        [Parameter] public SalesOrderEntity SalesOrder { get; set; }
        [Parameter] public EventCallback<bool> ChangeState { get; set; }
        [Inject] private ISalesOrderRepository SalesOrderRepository { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ISalesOrderVariantRepository SalesOrderVariantRepository { get; set; }
        [Inject] private ILogger<SalesOrderVariantComponent> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private bool isVisibleProductPopup = false;
        private ProductEntity product = new();
        private int serialnumber = 1;

        private SalesOrderVariantModel salesOrderVariantModel = new();
        private List<SalesOrderVariantModel> salesOrderVariants = new();
        private SalesOrderVariantEntity salesOrderVariantEntity = new();
        private bool isSortAscending = false;

        protected override void OnParametersSet()
        {
            if (SalesOrder.SalesOrderVariants.Count != 0)
            {
                serialnumber = SalesOrder.SalesOrderVariants.OrderByDescending(p => p.SerialNumber).First().SerialNumber + 1;
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            GetSalesOrderVariants();
            GetAmount();
        }

        public async Task AddVariant()
        {
            if (SalesOrder != null || salesOrderVariantModel != null)
            {
                try
                {
                    salesOrderVariantModel.SerialNumber = serialnumber;
                    salesOrderVariantEntity.SerialNumber = salesOrderVariantModel.SerialNumber;
                    salesOrderVariantEntity.VariantEntityId = salesOrderVariantModel.VariantEntityId.Value;
                    salesOrderVariantEntity.Quantity = salesOrderVariantModel.Quantity;
                    salesOrderVariantEntity.Amount = salesOrderVariantModel.Amount;
                    salesOrderVariantEntity.Discount = salesOrderVariantModel.Discount;
                    salesOrderVariantEntity.AmountAfterDiscount = salesOrderVariantModel.AmountAfterDiscount;
                    salesOrderVariantEntity.ProductRate = salesOrderVariantModel.ProductRate;
                    salesOrderVariantEntity.Remarks = salesOrderVariantModel.Remarks;
                    salesOrderVariantEntity.SalesOrderEntityId = SalesOrder.Id;

                    salesOrderVariantEntity.Product = product;
                    SalesOrder.SalesOrderVariants.Add(salesOrderVariantEntity);
                    await SalesOrderRepository.Update(SalesOrder);

                    //product.SalesOrderVariants.Add(salesOrderVariantEntity);
                    //await ProductRepository.Update(product);
                    CancelSalesOrderVariant();
                    GetSalesOrderVariants();
                    serialnumber += 1;
                    await ChangeState.InvokeAsync(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public async Task EditVariant()
        {
            try
            {
                if (salesOrderVariantModel != null)
                {
                    salesOrderVariantEntity = await SalesOrderVariantRepository.GetById(salesOrderVariantModel.Id);
                    if (salesOrderVariantEntity != null)
                    {
                        salesOrderVariantModel.SerialNumber = serialnumber;
                        salesOrderVariantEntity.SerialNumber = salesOrderVariantModel.SerialNumber;
                        salesOrderVariantEntity.VariantEntityId = salesOrderVariantModel.VariantEntityId.Value;
                        salesOrderVariantEntity.Quantity = salesOrderVariantModel.Quantity;
                        salesOrderVariantEntity.Amount = salesOrderVariantModel.Amount;
                        salesOrderVariantEntity.Discount = salesOrderVariantModel.Discount;
                        salesOrderVariantEntity.AmountAfterDiscount = salesOrderVariantModel.AmountAfterDiscount;
                        salesOrderVariantEntity.ProductRate = salesOrderVariantModel.ProductRate;
                        salesOrderVariantEntity.Remarks = salesOrderVariantModel.Remarks;
                        salesOrderVariantEntity.Product = product;

                        await SalesOrderVariantRepository.Update(salesOrderVariantEntity);
                    }
                    CancelSalesOrderVariant();
                    await ChangeState.InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Update salesorder variant error: " + ex.Message);
            }
        }

        public async Task DeleteVariant(Guid id)
        {
            try
            {
                var variant = await SalesOrderVariantRepository.GetById(id);
                if (variant != null)
                {
                    await SalesOrderVariantRepository.Delete(variant);
                    GetSalesOrderVariants();
                    await ChangeState.InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete sales order variant" + ex.Message);
            }
        }

        public void CancelSalesOrderVariant()
        {
            salesOrderVariantModel = new();
            product = new();
            salesOrderVariantEntity = new();
        }

        public void OpenProductPopup()
        {
            isVisibleProductPopup = true;
        }

        public void CloseProductPopup(bool state)
        {
            isVisibleProductPopup = state;
        }

        public async Task GetProductFromPopup(ProductModel productFromPopup)
        {
            if (productFromPopup != null)
            {
                salesOrderVariantModel.ProductRate = productFromPopup.Rate;
                salesOrderVariantModel.ProductId = productFromPopup.ProductId;
                salesOrderVariantModel.ProductName = productFromPopup.Name;
                salesOrderVariantModel.VariantEntityId = null;
                product = await ProductRepository.GetById(productFromPopup.Id);
            }
        }

        public void QuantityChanged(int? value)
        {
            salesOrderVariantModel.Quantity = value;
            GetAmount();
        }

        public void RateChanged(decimal? value)
        {
            salesOrderVariantModel.ProductRate = value;
            GetAmount();
        }

        public void GetAmount()
        {
            if (salesOrderVariantModel.ProductRate == 0 || salesOrderVariantModel.Quantity == 0)
                salesOrderVariantModel.Amount = 0;
            else
                salesOrderVariantModel.Amount = Math.Round(salesOrderVariantModel.ProductRate.Value * salesOrderVariantModel.Quantity.Value, 2);
            GetAmountAfterDiscount();
        }

        private void DiscountChanged(int? value)
        {
            salesOrderVariantModel.Discount = value;
            GetAmountAfterDiscount();
        }

        public void GetAmountAfterDiscount()
        {
            if (salesOrderVariantModel.Amount != 0 && salesOrderVariantModel.Discount != 0)
            {
                var amount =  salesOrderVariantModel.Amount - (salesOrderVariantModel.Amount * ((decimal)salesOrderVariantModel.Discount / 100));
                salesOrderVariantModel.AmountAfterDiscount = Math.Round(amount.Value, 2);
            }
            else if (salesOrderVariantModel.Amount != 0 && salesOrderVariantModel.Discount == 0)
            {
                salesOrderVariantModel.AmountAfterDiscount = Math.Round(salesOrderVariantModel.Amount.Value, 2);
            }
        }

        public void GetSalesOrderVariants()
        {
            salesOrderVariants = SalesOrder.SalesOrderVariants.Select(v => new SalesOrderVariantModel()
            {
                Amount = v.Amount,
                AmountAfterDiscount = v.AmountAfterDiscount,
                Discount = v.Discount,
                Id = v.Id,
                ProductId = v.Product.ProductId,
                ProductName = v.Product.Name,
                ProductRate = v.ProductRate,
                Quantity = v.Quantity,
                SerialNumber = v.SerialNumber,
                ProductVariantId = v.ProductVariant.VariantId,
                ProductEntityId = v.Product.Id,
                Remarks = v.Remarks,
                VariantEntityId = v.VariantEntityId,
                Product = v.Product
            }).ToList();
        }

        public async void UpdateSalesOrderVariant(SalesOrderVariantModel model)
        {
            CancelSalesOrderVariant();
            salesOrderVariantModel = model;
            serialnumber = model.SerialNumber;
            product = model.Product;
        }

        public void SortItem(string column)
        {
            if (salesOrderVariants.Count != 0)
            {
                if (column == "SerialNumber")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.SerialNumber).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.SerialNumber).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductId")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.ProductId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.ProductId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductName")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.ProductId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.ProductId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "VariantId")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.ProductVariantId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.ProductVariantId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Quantity")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.Quantity).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.Quantity).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductRate")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.ProductRate).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.ProductRate).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Amount")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.Amount).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.Amount).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Discount")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.Discount).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.Discount).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "AmmountAfterDiscount")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.AmountAfterDiscount).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.AmountAfterDiscount).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Remarks")
                {
                    if (isSortAscending)
                    {
                        salesOrderVariants = salesOrderVariants.OrderBy(c => c.Remarks).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrderVariants = salesOrderVariants.OrderByDescending(c => c.Remarks).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }
}
