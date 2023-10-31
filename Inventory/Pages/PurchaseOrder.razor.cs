using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class PurchaseOrder
    {
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ISupplierRepository SupplierRepository { get; set; }
        [Inject] private IPurchaseOrderRepository PurchaseOrderRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private PurchaseOrderEntity purchaseOrderEntity = new();
        private PurchaseOrderModel purchaseOrderModel = new();
        private ProductEntity product = new();
        private bool isVisibleSupplierPopup = false;




        protected async override Task OnInitializedAsync()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Logger.LogError("Purchase order error: " + ex.Message);
            }
        }

        public async Task AddPurchaseOrder()
        {

        }

        public async Task EditPurchaseOrder()
        {

        }

        public void CancelOrder()
        {

        }

        public void CloseSupplierPopup(bool state)
        {
            isVisibleSupplierPopup = state;
        }

        public void GetSupplierFromPopup(SupplierModel supplierFromPopup)
        {
            if (supplierFromPopup != null)
                purchaseOrderModel.SupplierId = supplierFromPopup.SupplierId;
        }
        public void SelectProduct()
        {

        }
    }
}
