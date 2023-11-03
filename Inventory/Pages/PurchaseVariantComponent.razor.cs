﻿using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Shared;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class PurchaseVariantComponent
    {
        [Parameter] public PurchaseEntity Purchase { get; set; }

        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
        [Inject] private IPurchaseVariantRepository PurchaseVariantRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private NavigationManager navManager { get; set; }

        private bool isVisibleProductPopup = false;
        private ProductEntity product = new();
        private int serialnumber = 1;

        private PurchaseVariantModel purchaseVariant = new();
        private List<PurchaseVariantModel> purchaseVariants = new();
        private PurchaseVariant purchaseVariantEntity = new();
        private bool isSortAscending = false;

        protected async override Task OnInitializedAsync()
        {
            GetPurchaseVariants();
            GetAmountAfterDiscount();

        }

        public async Task AddPurchaseVariant()
        {
            if (Purchase != null || purchaseVariant != null)
            {
                try
                {
                    purchaseVariant.SerialNumber = serialnumber;
                    purchaseVariantEntity.SerialNumber = purchaseVariant.SerialNumber;
                    purchaseVariantEntity.VariantId = purchaseVariant.VariantId;
                    purchaseVariantEntity.Quantity = purchaseVariant.Quantity;
                    purchaseVariantEntity.Amount = purchaseVariant.Amount;
                    purchaseVariantEntity.Discount = purchaseVariant.Discount;
                    purchaseVariantEntity.AmountAfterDiscount = purchaseVariant.AmountAfterDiscount;
                    purchaseVariantEntity.ProductRate = purchaseVariant.ProductRate;
                    purchaseVariantEntity.PurchaseEntityId = Purchase.Id;

                    product.PurchaseVariants.Add(purchaseVariantEntity);
                    await ProductRepository.Update(product);
                    CancelPurchaseVariant();
                    GetPurchaseVariants();
                    serialnumber += 1;
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

            }
            catch (Exception ex)
            {
                Logger.LogError("Update purchase error: " + ex.Message);
            }
        }

        public void CancelPurchaseVariant()
        {
            purchaseVariant = new();
            product = new();
            purchaseVariantEntity = new();
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
                purchaseVariant.ProductRate = productFromPopup.Rate;
                purchaseVariant.ProductId = productFromPopup.ProductId;
                purchaseVariant.ProductName = productFromPopup.Name;
                product = await ProductRepository.GetById(productFromPopup.Id);
            }
        }

        private void AmountChanged(decimal? value)
        {
            purchaseVariant.Amount = value;
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
                purchaseVariant.AmountAfterDiscount = purchaseVariant.Amount - (purchaseVariant.Amount * ((decimal)purchaseVariant.Discount / 100));
            }
            else if (purchaseVariant.Amount != 0 && purchaseVariant.Discount == 0)
            {
                purchaseVariant.AmountAfterDiscount = purchaseVariant.Amount;
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
                VariantId = v.VariantId
            }).ToList();
        }

        public void UpdatePurchaseVariant(PurchaseVariantModel model)
        {
            CancelPurchaseVariant();
            purchaseVariant= model;
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
                }
            }
            catch (Exception)
            {

                throw;
            }
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
                        purchaseVariants = purchaseVariants.OrderBy(c => c.VariantId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchaseVariants = purchaseVariants.OrderByDescending(c => c.VariantId).ToList();
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
    }
}