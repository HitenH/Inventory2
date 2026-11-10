using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Inventory.MudBlazorComponents;
using Inventory.Shared.Selectors;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace Inventory.Shared
{
    public partial class PurchaseVariantComponent
    {
        [Parameter] public PurchaseEntity Purchase { get; set; }
        [Parameter] public EventCallback<bool> ChangeState { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private IPurchaseVariantRepository PurchaseVariantRepository { get; set; }
        [Inject] private IDialogService DialogService { get; set; }
        [Inject] private IJSRuntime JSRuntime { get; set; }
        [Inject] private ISnackbar Snackbar { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private ILogger<PurchaseVariantComponent> Logger { get; set; }

        private ProductEntity product = new();
        private ProductModel selectedProduct;
        private int serialnumber = 1;

        private PurchaseVariantModel purchaseVariant = new();
        private List<PurchaseVariantModel> purchaseVariants = new();
        private PurchaseVariant purchaseVariantEntity = new();
        private bool isSortAscending = false;
        private ProductAutocomplete<ProductModel> productAutocompleteReference;

        protected override void OnParametersSet()   
        {
            Purchase.PurchaseVariants ??= new List<PurchaseVariant>();
            Purchase.PurchaseVariants = [.. Purchase.PurchaseVariants.OrderBy(o => o.SerialNumber)];

            if (Purchase.PurchaseVariants.Count != 0)
            {
                serialnumber = Purchase.PurchaseVariants.OrderByDescending(p => p.SerialNumber).First().SerialNumber + 1;
            }

            GetPurchaseVariants();
        }

        public async Task AddPurchaseVariant()
        {
            if (Purchase?.Id == Guid.Empty || product?.Id == Guid.Empty || purchaseVariant?.VariantEntytiId is null)
            {
                Snackbar.Add("Select a product and variant before adding a line.", Severity.Warning);
                return;
            }

            try
            {
                purchaseVariant.SerialNumber = serialnumber;
                await PurchaseVariantRepository.Create(new PurchaseVariant
                {
                    PurchaseEntityId = Purchase.Id,
                    ProductEntityId = product.Id,
                    VariantEntityId = purchaseVariant.VariantEntytiId.Value,
                    SerialNumber = serialnumber,
                    Quantity = purchaseVariant.Quantity,
                    Amount = purchaseVariant.Amount,
                    Discount = purchaseVariant.Discount,
                    AmountAfterDiscount = purchaseVariant.AmountAfterDiscount,
                    ProductRate = purchaseVariant.ProductRate
                });

                CancelPurchaseVariant();
                serialnumber += 1;
                await ChangeState.InvokeAsync(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Add purchase line failed");
                Snackbar.Add($"Could not add line: {ex.Message}", Severity.Error);
            }
        }

        public async Task EditPurchaseVariant()
        {
            if (purchaseVariant?.Id == Guid.Empty || product?.Id == Guid.Empty || purchaseVariant.VariantEntytiId is null)
            {
                Snackbar.Add("Select a product and variant before saving.", Severity.Warning);
                return;
            }

            try
            {
                await PurchaseVariantRepository.Update(new PurchaseVariant
                {
                    Id = purchaseVariant.Id,
                    PurchaseEntityId = Purchase.Id,
                    ProductEntityId = product.Id,
                    VariantEntityId = purchaseVariant.VariantEntytiId.Value,
                    SerialNumber = purchaseVariant.SerialNumber,
                    Quantity = purchaseVariant.Quantity,
                    Amount = purchaseVariant.Amount,
                    Discount = purchaseVariant.Discount,
                    AmountAfterDiscount = purchaseVariant.AmountAfterDiscount,
                    ProductRate = purchaseVariant.ProductRate
                });

                CancelPurchaseVariant();
                await ChangeState.InvokeAsync(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Update purchase line failed");
                Snackbar.Add($"Could not update line: {ex.Message}", Severity.Error);
            }
        }


        private async Task FocusInput()
        {
            await productAutocompleteReference.SetFocusAsync();
        }


        public async Task DeletePurchaseVariant(Guid id)
        {
            try
            {
                await PurchaseVariantRepository.Delete(new PurchaseVariant { Id = id });
                await ChangeState.InvokeAsync(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete purchase line failed");
                Snackbar.Add($"Could not delete line: {ex.Message}", Severity.Error);
            }
        }

        public void CancelPurchaseVariant()
        {
            selectedProduct = new();
            product = new();
            purchaseVariant.ProductId = "";
            purchaseVariant.ProductName = "";
            purchaseVariant = new();
            product = new();
            purchaseVariantEntity = new();
        }

        public void QuantityChanged(int? value)
        {
            purchaseVariant.Quantity = value;
            GetAmount();
        }

        public void RateChanged(decimal? value)
        {
            purchaseVariant.ProductRate = value;
            GetAmount();
        }

        public void GetAmount()
        {
            purchaseVariant.Amount = LineAmounts.ComputeAmount(purchaseVariant.ProductRate, purchaseVariant.Quantity);
            purchaseVariant.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(purchaseVariant.Amount, purchaseVariant.Discount);
        }

        private void DiscountChanged(int? value)
        {
            purchaseVariant.Discount = value;
            purchaseVariant.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(purchaseVariant.Amount, purchaseVariant.Discount);
        }

        public void GetPurchaseVariants()
        {
            purchaseVariants = Purchase.PurchaseVariants.Select(v => new PurchaseVariantModel()
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
                VariantEntytiId = v.VariantEntityId,
                ProductVariantId = v.ProductVariant?.VariantId ?? "",
                ProductEntityId = v.ProductEntityId,
                Product = v.Product
            }).ToList();
        }

        public async void UpdatePurchaseVariant(PurchaseVariantModel model)
        {
            CancelPurchaseVariant();
            purchaseVariant = model;
            serialnumber= model.SerialNumber;
            product = model.Product;
        }

        public void SortItem(string column)
        {
            if (purchaseVariants.Count != 0)
            {
                if (column == "SerialNumber")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.SerialNumber).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.SerialNumber).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductId")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.ProductId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.ProductId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductName")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.ProductName).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.ProductName).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "VariantId")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.ProductVariantId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.ProductVariantId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Quantity")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.Quantity).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.Quantity).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductRate")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.ProductRate).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.ProductRate).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Amount")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.Amount).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.Amount).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Discount")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.Discount).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.Discount).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "AmmountAfterDiscount")
                {
                    if (isSortAscending)
                    {
                        purchaseVariants = purchaseVariants.OrderBy(c => c.AmountAfterDiscount).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.AmountAfterDiscount).ToList();
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
                purchaseVariant.ProductId = _product.ProductId;
                purchaseVariant.ProductName = _product.Name;
            }
        }
    }
}
