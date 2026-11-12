using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Shared
{
    public partial class SalesOrderVariantComponent
    {
        [Parameter] public SalesOrderEntity SalesOrder { get; set; }
        [Parameter] public EventCallback<bool> ChangeState { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ISalesOrderVariantRepository SalesOrderVariantRepository { get; set; }
        [Inject] private ILogger<SalesOrderVariantComponent> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IDialogService DialogService { get; set; }
        [Inject] private ISnackbar Snackbar { get; set; }

        private ProductEntity product = new();
        private int serialnumber = 1;

        private SalesOrderVariantModel salesOrderVariantModel = new();
        private List<SalesOrderVariantModel> salesOrderVariants = new();
        private SalesOrderVariantEntity salesOrderVariantEntity = new();
        private bool isSortAscending = false;


        private List<ProductModel> products = new();
        private List<ProductModel> productsAfterSearch = new();
        private ProductModel selectedProduct;

        protected override void OnParametersSet()
        {
            SalesOrder.SalesOrderVariants ??= new List<SalesOrderVariantEntity>();
            SalesOrder.SalesOrderVariants = [.. SalesOrder.SalesOrderVariants.OrderBy(o => o.SerialNumber)];

            if (SalesOrder.SalesOrderVariants.Count != 0)
            {
                serialnumber = SalesOrder.SalesOrderVariants.Max(p => p.SerialNumber) + 1;
            }

            GetSalesOrderVariants();
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

                    await SalesOrderVariantRepository.Create(new SalesOrderVariantEntity
                    {
                        SalesOrderEntityId = SalesOrder.Id,
                        ProductEntityId = product.Id,
                        VariantEntityId = salesOrderVariantModel.VariantEntityId.Value,
                        SerialNumber = serialnumber,
                        Quantity = salesOrderVariantModel.Quantity,
                        ProductRate = salesOrderVariantModel.ProductRate,
                        Amount = salesOrderVariantModel.Amount,
                        Discount = salesOrderVariantModel.Discount,
                        AmountAfterDiscount = salesOrderVariantModel.AmountAfterDiscount,
                        Remarks = salesOrderVariantModel.Remarks
                    });

                    CancelSalesOrderVariant();
                    serialnumber += 1;
                    await ChangeState.InvokeAsync(true);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Add sales order line failed");
                    Snackbar.Add($"Could not add line: {ex.Message}", Severity.Error);
                }
            }
        }

        public async Task EditVariant()
        {
            try
            {
                if (salesOrderVariantModel != null)
                {
                    await SalesOrderVariantRepository.Update(new SalesOrderVariantEntity
                    {
                        Id = salesOrderVariantModel.Id,
                        SalesOrderEntityId = SalesOrder.Id,
                        ProductEntityId = product.Id,
                        VariantEntityId = salesOrderVariantModel.VariantEntityId.Value,
                        SerialNumber = salesOrderVariantModel.SerialNumber,
                        Quantity = salesOrderVariantModel.Quantity,
                        ProductRate = salesOrderVariantModel.ProductRate,
                        Amount = salesOrderVariantModel.Amount,
                        Discount = salesOrderVariantModel.Discount,
                        AmountAfterDiscount = salesOrderVariantModel.AmountAfterDiscount,
                        Remarks = salesOrderVariantModel.Remarks
                    });

                    CancelSalesOrderVariant();
                    await ChangeState.InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Update salesorder variant error");
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
                Logger.LogError(ex, "Delete sales order variant");
            }
        }

        public void CancelSalesOrderVariant()
        {
            salesOrderVariantModel = new();
            product = new();
            salesOrderVariantEntity = new();
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
            salesOrderVariantModel.Amount = LineAmounts.ComputeAmount(salesOrderVariantModel.ProductRate, salesOrderVariantModel.Quantity);
            salesOrderVariantModel.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(salesOrderVariantModel.Amount, salesOrderVariantModel.Discount);
        }

        private void DiscountChanged(int? value)
        {
            salesOrderVariantModel.Discount = value;
            salesOrderVariantModel.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(salesOrderVariantModel.Amount, salesOrderVariantModel.Discount);
        }

        public void GetSalesOrderVariants()
        {
            salesOrderVariants = SalesOrder.SalesOrderVariants.Select(v => new SalesOrderVariantModel()
            {
                Amount = v.Amount,
                AmountAfterDiscount = v.AmountAfterDiscount,
                Discount = v.Discount,
                Id = v.Id,
                ProductId = v.Product?.ProductId ?? "",
                ProductName = v.Product?.Name ?? "",
                ProductRate = v.ProductRate,
                Quantity = v.Quantity,
                SerialNumber = v.SerialNumber,
                ProductVariantId = v.ProductVariant?.VariantId ?? "",
                ProductEntityId = v.ProductEntityId,
                Remarks = v.Remarks,
                VariantEntityId = v.VariantEntityId,
                Product = v.Product
            }).ToList();
        }

        public void UpdateSalesOrderVariant(SalesOrderVariantModel model)
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

        private async Task OnProductSelected(ProductModel? _product)
        {
            if (_product != null)
            {
                selectedProduct = _product;
                product = await ProductRepository.GetProductWithVariantsById(_product.Id);
                salesOrderVariantModel.ProductId = _product.ProductId;
                salesOrderVariantModel.ProductName = _product.Name;
            }
        }
    }
}
