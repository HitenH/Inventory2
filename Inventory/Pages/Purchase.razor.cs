using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;
using System;

namespace Inventory.Pages
{
    public partial class Purchase
    {
        [Parameter] public string PurchaseId { get; set; }
        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
        //[Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ISupplierRepository SuplierRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private bool isVisibleSupplierPopup = false;
        //private bool isVisibleProductPopup = false;
        // private SupplierModel supplier = new();
        // private ProductEntity product = new();
        private PurchaseModel purchaseModel = new();
        private PurchaseEntity purchaseEntity = new();
        private PurchaseTotalData PurchaseTotalData = new();
        // private int serialnumber = 1;
        //private PurchaseVariantModel purchaseVariant = new();
        //private PurchaseVariant purchaseVariantEntity = new();



        protected async override Task OnInitializedAsync()
        {
            if (PurchaseId != null)
            {
                purchaseEntity = await PurchaseRepository.GetById(Guid.Parse(PurchaseId));
                if (purchaseEntity != null)
                {
                    purchaseModel.SupplierEntityId = purchaseEntity.SupplierEntityId;
                    purchaseModel.SupplierId = purchaseEntity.Supplier.SupplierId;
                    purchaseModel.SupplierName = purchaseEntity.Supplier.Name;
                    purchaseModel.Date = purchaseEntity.Date;
                    purchaseModel.Remarks = purchaseEntity.Remarks;
                    purchaseModel.TotalAmountProduct = purchaseEntity.TotalAmountProduct;
                    purchaseModel.VoucherId = purchaseEntity.VoucherId;
                }
            }
            //GetAmountAfterDiscount();

        }
        public async Task AddPurchase()
        {
            if (purchaseModel != null)
            {
                try
                {
                    var voucherIdDb = await PurchaseRepository.GetLastVoucherId();
                    if (voucherIdDb == 0)
                        purchaseModel.VoucherId = 1;
                    else
                        purchaseModel.VoucherId = voucherIdDb + 1;

                    purchaseEntity = new()
                    {
                        Date = purchaseModel.Date,
                        Remarks = purchaseModel.Remarks,
                        SupplierEntityId = purchaseModel.SupplierEntityId,
                        VoucherId = purchaseModel.VoucherId
                    };
                    var id = await PurchaseRepository.Create(purchaseEntity);
                    PurchaseId = id.ToString();
                    purchaseModel.Id = id;
                    purchaseEntity = await PurchaseRepository.GetById(id);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Add new purchase error: " + ex.Message);
                }
            }
        }
        public async Task EditPurchase()
        {
            if (purchaseModel != null)
            {
                try
                {
                    var isVoucherExist = false;
                    if (purchaseEntity.VoucherId != purchaseModel.VoucherId)
                        isVoucherExist = await PurchaseRepository.IsVoucherExist(purchaseModel.VoucherId);

                    if (!isVoucherExist)
                    {
                        if (purchaseEntity.VoucherId != purchaseModel.VoucherId)
                            purchaseEntity.VoucherId = purchaseModel.VoucherId;

                        purchaseEntity.SupplierEntityId = purchaseModel.SupplierEntityId;
                        purchaseEntity.Remarks = purchaseModel.Remarks;
                        purchaseEntity.Date = purchaseModel.Date;

                        var id = await PurchaseRepository.Update(purchaseEntity);
                    }
                    else
                        purchaseModel.VoucherId = purchaseEntity.VoucherId;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update purchase: " + ex.Message);
                }
            }
        }

        public void CancelPurchase()
        {
            purchaseModel = new();
        }

        public async Task AddTotalAmount()
        {

        }

        public void OpenSupplierPopup()
        {
            isVisibleSupplierPopup = true;
        }
        public void CloseSupplierPopup(bool state)
        {
            isVisibleSupplierPopup = state;
        }
        public void GetSupplierFromPopup(SupplierModel supplierFromPopup)
        {
            if (supplierFromPopup != null)
            {
                purchaseModel.SupplierEntityId = supplierFromPopup.Id;
                purchaseModel.SupplierId = supplierFromPopup.SupplierId;
                purchaseModel.SupplierName = supplierFromPopup.Name;
            }


        }







        //public async Task AddPurchaseVariant()
        //{
        //    if (purchaseModel != null || purchaseVariant != null)
        //    {

        //        try
        //        {
        //            purchaseVariant.SerialNumber = serialnumber;

        //            var variant = Mapper.Map<PurchaseVariant>(purchaseVariant);


        //            product.PurchaseVariants.Add(variant);
        //            await ProductRepository.Update(product);

        //            purchaseEntity.PurchaseVariants.Add(variant);
        //            await PurchaseRepository.Update(purchaseEntity);
        //            CancelPurchaseVariant();
        //            serialnumber += 1;
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine(ex.Message);
        //        }

        //    }
        //}

        //public async Task EditPurchaseVariant()
        //{
        //    try
        //    {

        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogError("Update purchase error: " + ex.Message);
        //    }
        //}


        //public void OpenProductPopup()
        //{
        //    isVisibleProductPopup = true;
        //    isVisibleSupplierPopup = false;
        //}

        //public void CancelPurchaseVariant()
        //{
        //    purchaseVariant = new();
        //    product = new();
        //}

        //public void CloseProductPopup(bool state)
        //{
        //    isVisibleProductPopup = state;
        //}



        //public async Task GetProductFromPopup(ProductModel productFromPopup)
        //{
        //    if (productFromPopup != null)
        //    {
        //        product = await ProductRepository.GetById(productFromPopup.Id);
        //        purchaseVariant.ProductRate = productFromPopup.Rate;
        //    }
        //}

        //private void AmountChanged(decimal? value)
        //{
        //    purchaseVariant.Amount = value;
        //    GetAmountAfterDiscount();
        //}

        //private void DiscountChanged(int? value)
        //{
        //    purchaseVariant.Discount = value;
        //    GetAmountAfterDiscount();
        //}


        //public void GetAmountAfterDiscount()
        //{
        //    if (purchaseVariant.Amount != 0 && purchaseVariant.Discount != 0)
        //    {
        //        purchaseVariant.AmountAfterDiscount = purchaseVariant.Amount - (purchaseVariant.Amount * ((decimal)purchaseVariant.Discount / 100));
        //    }
        //    else if (purchaseVariant.Amount != 0 && purchaseVariant.Discount == 0)
        //    {
        //        purchaseVariant.AmountAfterDiscount = purchaseVariant.Amount;
        //    }
        //}
    }
}
