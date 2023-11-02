//using AutoMapper;
//using Inventory.Domain.Entities;
//using Inventory.Domain.Repository.Abstract;
//using Inventory.Models;
//using Microsoft.AspNetCore.Components;

//namespace Inventory.Pages
//{
//    public partial class Purchase
//    {
//        [Parameter] public string PurchaseId { get; set; }
//        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
//        [Inject] private IProductRepository ProductRepository { get; set; }
//        [Inject] private ILogger<Login> Logger { get; set; }
//        [Inject] private NavigationManager navManager { get; set; }
//        [Inject] private IMapper Mapper { get; set; }

//        private bool isVisibleSupplierPopup = false;
//        private bool isVisibleProductPopup = false;
//        private SupplierModel supplier = new();
//        private PurchaseModel purchaseModel = new();
//        private PurchaseEntity purchaseEntity = new();
//        private DateOnly selectedData;
//        private int serialnumber = 1;
//        private PurchaseVariant purchaseVariant = new();
//        private ProductEntity product = new();



//        protected async override Task OnInitializedAsync()
//        {
//            purchaseVariant = new PurchaseVariant()
//            {
//                Product = new()
//            };
//            selectedData = DateOnly.FromDateTime(DateTime.Now);
//            GetAmountAfterDiscount();
//            if (PurchaseId != null)
//            {
//                purchaseEntity = await PurchaseRepository.GetById(Guid.Parse(PurchaseId));
//                if (purchaseEntity != null)
//                    purchaseModel = Mapper.Map<PurchaseModel>(purchaseEntity);
//            }
//        }

//        public async Task AddPurchase()
//        {
//            try
//            {
//                if (purchaseModel is not null)
//                {
//                    purchaseEntity = Mapper.Map<PurchaseEntity>(purchaseModel);
//                    purchaseEntity.Supplier = Mapper.Map<SupplierEntity>(supplier);
//                    purchaseEntity.Date = selectedData;
//                    await PurchaseRepository.Create(purchaseEntity);
//                    CancelPurchase();
//                }
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError("Add new purchase error: " + ex.Message);
//            }
//        }
//        public async Task EditPurchase()
//        {

//        }
//        public async Task AddPurchaseVariant()
//        {
//            if (purchaseModel != null && purchaseVariant != null)
//            {
//                purchaseVariant.SerialNumber = serialnumber;
//                purchaseModel.PurchaseVariants.Add(purchaseVariant);
//                CancelPurchaseVariant();
//                serialnumber += 1;
//            }
//        }

//        public async Task EditPurchaseVariant()
//        {
//            try
//            {
//                //if (purchaseModel is not null)
//                //{
//                //    purchaseEntity.Date = selectedData;
//                //    purchaseEntity.Product = purchaseModel.Product;
//                //    purchaseEntity.SerialNumber = purchaseModel.SerialNumber;
//                //    purchaseEntity.VariantId = purchaseModel.VariantId;
//                //    purchaseEntity.Quantity = purchaseModel.Quantity;
//                //    purchaseEntity.Amount = purchaseModel.Amount;
//                //    purchaseEntity.AmountAfterDiscount = purchaseModel.AmountAfterDiscount;
//                //    purchaseEntity.Discount = purchaseModel.Discount;
//                //    purchaseEntity.Remarks = purchaseModel.Remarks;
//                //    purchaseEntity.VoucherId = purchaseModel.VoucherId;
//                //    purchaseEntity.ProductRate = purchaseModel.ProductRate;

//                //    await PurchaseRepository.Update(purchaseEntity);
//                //    CancelPurchase();
//                //}
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError("Update purchase error: " + ex.Message);
//            }
//        }

//        public void CancelPurchase()
//        {
//            purchaseModel = new();
//        }
//        public void CancelPurchaseVariant()
//        {
//            purchaseVariant = new();
//        }
//        public void OpenSupplierPopup()
//        {
//            isVisibleSupplierPopup = true;
//            isVisibleProductPopup = false;
//        }

//        public void OpenProductPopup()
//        {
//            isVisibleProductPopup = true;
//            isVisibleSupplierPopup = false;
//        }

//        public void CloseSupplierPopup(bool state)
//        {
//            isVisibleSupplierPopup = state;
//        }

//        public void CloseProductPopup(bool state)
//        {
//            isVisibleProductPopup = state;
//        }

//        public void GetSupplierFromPopup(SupplierModel supplierFromPopup)
//        {
//            if (supplierFromPopup != null)
//                supplier = supplierFromPopup;
//        }

//        public async Task GetProductFromPopup(ProductModel productFromPopup)
//        {
//            if (productFromPopup != null)
//            {
//                product = await ProductRepository.GetById(productFromPopup.Id);

//                purchaseVariant.Product = product;
//                purchaseVariant.ProductRate = productFromPopup.Rate;
//            }
//        }

//        private void AmountChanged(decimal? value)
//        {
//            purchaseVariant.Amount = value;
//            GetAmountAfterDiscount();
//        }

//        private void DiscountChanged(int? value)
//        {
//            purchaseVariant.Discount = value;
//            GetAmountAfterDiscount();
//        }


//        public void GetAmountAfterDiscount()
//        {
//            if (purchaseVariant.Amount != 0 && purchaseVariant.Discount != 0)
//            {
//                purchaseVariant.AmountAfterDiscount = purchaseVariant.Amount - (purchaseVariant.Amount * ((decimal)purchaseVariant.Discount / 100));
//            }
//            else if (purchaseVariant.Amount != 0 && purchaseVariant.Discount == 0)
//            {
//                purchaseVariant.AmountAfterDiscount = purchaseVariant.Amount;
//            }
//        }
//    }
//}
