using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
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
        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
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
            if (Purchase.PurchaseVariants.Count != 0)
            {
                serialnumber = Purchase.PurchaseVariants.OrderBy(p => p.SerialNumber).First().SerialNumber + 1;
            }
        }
        protected override void OnAfterRender(bool firstRender)
        {
            GetPurchaseVariants();
            GetAmount();
        }

        public async Task AddPurchaseVariant()
        {
            if (Purchase != null && purchaseVariant != null)
            {
                try
                {
                    purchaseVariant.SerialNumber = serialnumber;
                    purchaseVariantEntity.SerialNumber = purchaseVariant.SerialNumber;
                    purchaseVariantEntity.VariantEntityId = purchaseVariant.VariantEntytiId.Value;
                    purchaseVariantEntity.Quantity = purchaseVariant.Quantity;
                    purchaseVariantEntity.Amount = purchaseVariant.Amount;
                    purchaseVariantEntity.Discount = purchaseVariant.Discount;
                    purchaseVariantEntity.AmountAfterDiscount = purchaseVariant.AmountAfterDiscount;
                    purchaseVariantEntity.ProductRate = purchaseVariant.ProductRate;
                    purchaseVariantEntity.PurchaseEntityId = Purchase.Id;

                    purchaseVariantEntity.Product = product;

                    Purchase.PurchaseVariants.Add(purchaseVariantEntity);
                    await PurchaseRepository.Update(Purchase);

                    CancelPurchaseVariant();
                    GetPurchaseVariants();
                    serialnumber += 1;
                    await ChangeState.InvokeAsync(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public async Task EditPurchaseVariant()
        {
            try
            {
                if (purchaseVariant != null)
                {
                    purchaseVariantEntity = await PurchaseVariantRepository.GetById(purchaseVariant.Id);
                    if (purchaseVariantEntity != null)
                    {
                        purchaseVariant.SerialNumber = serialnumber;
                        purchaseVariantEntity.SerialNumber = purchaseVariant.SerialNumber;
                        purchaseVariantEntity.VariantEntityId = purchaseVariant.VariantEntytiId.Value;
                        purchaseVariantEntity.Quantity = purchaseVariant.Quantity;
                        purchaseVariantEntity.Amount = purchaseVariant.Amount;
                        purchaseVariantEntity.Discount = purchaseVariant.Discount;
                        purchaseVariantEntity.AmountAfterDiscount = purchaseVariant.AmountAfterDiscount;
                        purchaseVariantEntity.ProductRate = purchaseVariant.ProductRate;

                        purchaseVariantEntity.Product = product;

                        await PurchaseVariantRepository.Update(purchaseVariantEntity);
                    }
                    CancelPurchaseVariant();
                    await ChangeState.InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Update purchase error: " + ex.Message);
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
                var variant = await PurchaseVariantRepository.GetById(id);
                if (variant != null)
                {
                    await PurchaseVariantRepository.Delete(variant);
                    GetPurchaseVariants();
                    await ChangeState.InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete purchase variant" + ex.Message);
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
            if (purchaseVariant.ProductRate == 0 || purchaseVariant.Quantity == 0)
                purchaseVariant.Amount = 0;
            else
                purchaseVariant.Amount = Math.Round(purchaseVariant.ProductRate.Value * purchaseVariant.Quantity.Value, 2);
            GetAmountAfterDiscount();
        }

        private void DiscountChanged(int? value)
        {
            purchaseVariant.Discount = value;
            GetAmountAfterDiscount();
        }


        public void GetAmountAfterDiscount()
        {
            if (purchaseVariant.Amount != 0 && purchaseVariant.Discount != 0)
            {
                var amount = purchaseVariant.Amount - purchaseVariant.Amount * ((decimal)purchaseVariant.Discount / 100);
                purchaseVariant.AmountAfterDiscount = Math.Round(amount.Value, 2);
            }
            else if (purchaseVariant.Amount != 0 && purchaseVariant.Discount == 0)
            {
                purchaseVariant.AmountAfterDiscount = Math.Round(purchaseVariant.Amount.Value, 2);
            }
        }

        public void GetPurchaseVariants()
        {
            purchaseVariants = Purchase.PurchaseVariants.Select(v => new PurchaseVariantModel()
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
                VariantEntytiId = v.VariantEntityId,
                ProductVariantId = v.ProductVariant.VariantId,
                ProductEntityId = v.Product.Id,
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
                        purchaseVariants = purchaseVariants.OrderBy(c => c.ProductId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.ProductId).ToList();
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
