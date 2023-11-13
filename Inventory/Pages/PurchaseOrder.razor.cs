using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Shared;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class PurchaseOrder
    {
        [Inject] private IPurchaseOrderRepository PurchaseOrderRepository { get; set; }
        [Inject] private ILogger<PurchaseOrder> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private PurchaseOrderEntity purchaseOrderEntity = new();
        private PurchaseOrderModel purchaseOrderModel = new();
        private List<PurchaseOrderModel> orders = new();
        private List<PurchaseOrderModel> ordersAfterSearch = new();
        private List<PurchaseOrderEntity> ordersDb = new();
        private ProductModel product = new();
        private bool isVisibleSupplierPopup = false;
        private bool isVisibleProductPopup = false;
        private bool isSortAscending = false;
        private bool isSelected = false;


        protected async override Task OnInitializedAsync()
        {
            await GetOrders();
        }

        public async Task GetOrders()
        {
            try
            {
                ordersDb = await PurchaseOrderRepository.GetAll();
                //orders = ordersDb.Select(o => Mapper.Map<PurchaseOrderModel>(o)).ToList();
                orders = ordersDb.Select(o => new PurchaseOrderModel()
                {
                    Date = o.Date,
                    DueDate= o.DueDate,
                    Id= o.Id,
                    OrderStatus= o.OrderStatus,
                    ProductId= o.ProductId,
                    ProductName= o.ProductName,
                    ProductRate= o.ProductRate,
                    Quantity= o.Quantity,
                    Remarks= o.Remarks,
                    SupplierId= o.SupplierId,
                    VariantId= o.VariantId
                }).ToList();
                ordersAfterSearch = orders;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError("Purchase order error: " + ex.Message);
            }
        }
        public async Task AddPurchaseOrder()
        {
            if (purchaseOrderModel != null)
            {
                try
                {
                    purchaseOrderEntity.Quantity = purchaseOrderModel.Quantity;
                    purchaseOrderEntity.VariantId = purchaseOrderModel.VariantId;
                    purchaseOrderEntity.ProductId = purchaseOrderModel.ProductId;
                    purchaseOrderEntity.ProductName = purchaseOrderModel.ProductName;
                    purchaseOrderEntity.OrderStatus = purchaseOrderModel.OrderStatus;
                    purchaseOrderEntity.Date = purchaseOrderModel.Date;
                    purchaseOrderEntity.DueDate = purchaseOrderModel.DueDate;
                    purchaseOrderEntity.Remarks= purchaseOrderModel.Remarks;
                    purchaseOrderEntity.ProductRate= purchaseOrderModel.ProductRate;
                    purchaseOrderEntity.SupplierId = purchaseOrderModel.SupplierId;

                    await PurchaseOrderRepository.Create(purchaseOrderEntity);
                    CancelOrder();
                    await GetOrders();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Add new order error: " + ex.Message);
                }
            }
        }

        public async Task EditPurchaseOrder()
        {
            if (purchaseOrderModel != null)
            {
                try
                {
                    purchaseOrderEntity = await PurchaseOrderRepository.GetById(purchaseOrderModel.Id);
                    if (purchaseOrderEntity != null)
                    {
                        purchaseOrderEntity.SupplierId = purchaseOrderModel.SupplierId;
                        purchaseOrderEntity.Date = purchaseOrderModel.Date;
                        purchaseOrderEntity.ProductId = purchaseOrderModel.ProductId;
                        purchaseOrderEntity.ProductName = purchaseOrderModel.ProductName;
                        purchaseOrderEntity.Quantity = purchaseOrderModel.Quantity;
                        purchaseOrderEntity.ProductRate = purchaseOrderModel.ProductRate;
                        purchaseOrderEntity.OrderStatus = purchaseOrderModel.OrderStatus;
                        purchaseOrderEntity.VariantId = purchaseOrderModel.VariantId;
                        purchaseOrderEntity.Remarks = purchaseOrderModel.Remarks;
                        purchaseOrderEntity.DueDate = purchaseOrderModel.DueDate;

                        await PurchaseOrderRepository.Update(purchaseOrderEntity);
                        CancelOrder();
                        await GetOrders();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update order error: " + ex.Message);
                }
            }
        }

        public void UpdateOrder(PurchaseOrderModel model)
        {
            CancelOrder();
            purchaseOrderModel = model;
        }

        public void CancelOrder()
        {
            purchaseOrderModel = new();
            purchaseOrderEntity= new();
        }

        public void OpenSupplierPopup()
        {
            isVisibleSupplierPopup = true;
            isVisibleProductPopup= false;
        }

        public void OpenProductPopup()
        {
            isVisibleProductPopup = true;
            isVisibleSupplierPopup = false;
        }

        public void CloseSupplierPopup(bool state)
        {
            isVisibleSupplierPopup = state;
        }

        public void CloseProductPopup(bool state)
        {
            isVisibleProductPopup = state;
        }

        public void GetSupplierFromPopup(SupplierModel supplierFromPopup)
        {
            if (supplierFromPopup != null)
                purchaseOrderModel.SupplierId = supplierFromPopup.SupplierId;
        }

        public void GetProductFromPopup(ProductModel productFromPopup)
        {
            if (productFromPopup != null)
            {
                product = productFromPopup;
                purchaseOrderModel.ProductId = productFromPopup.ProductId;
                purchaseOrderModel.ProductName = productFromPopup.Name;
                purchaseOrderModel.ProductRate = productFromPopup.Rate;
            }
        }

        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            ordersAfterSearch = orders.Where(n => n.SupplierId.ToString().ToLower().Contains(search)
                                               || n.ProductId.ToString().ToLower().Contains(search)
                                               || n.ProductName.ToLower().Contains(search)
                                               || n.Quantity.ToString().Contains(search)
                                               || n.OrderStatus.ToString().ToLower().Contains(search)
                                               || n.ProductRate.ToString().Contains(search)).ToList();
        }

        public void SortItem(string column)
        {
            if (ordersAfterSearch.Count != 0)
            {
                if (column == "Date")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.Date).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.Date).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "SupplierId")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.SupplierId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.SupplierId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductId")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.ProductId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.ProductId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductName")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.ProductName).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.ProductName).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "VariantId")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.VariantId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.VariantId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Quantity")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.Quantity).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.Quantity).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "DueDate")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.DueDate).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.DueDate).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "ProductRate")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.ProductRate).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.ProductRate).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "OrderStatus")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.OrderStatus).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.OrderStatus).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Remarks")
                {
                    if (isSortAscending)
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderBy(c => c.Remarks).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        ordersAfterSearch = ordersAfterSearch.OrderByDescending(c => c.Remarks).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }

        public async void SelectAllItems()
        {
            isSelected = !isSelected;
            if (isSelected)
            {
                foreach (var order in ordersAfterSearch)
                {
                    order.IsChecked = true;
                }
            }
            else
            {
                foreach (var order in ordersAfterSearch)
                {
                    order.IsChecked = false;
                }
            }
        }

        public async Task DeleteSelectedOrders()
        {
            if (ordersAfterSearch.Count > 0)
            {
                try
                {
                    ordersDb = ordersDb.Where(o => ordersAfterSearch.Where(or => or.IsChecked == true).Any(p => p.Id == o.Id)).ToList();
                    await PurchaseOrderRepository.DeleteRange(ordersDb);
                    await GetOrders();
                    isSelected= false;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete selected orders error:" + ex.Message);
                }
            }
        }
        public async Task DeleteCompletedOrders()
        {
            if (ordersAfterSearch.Count > 0)
            {
                try
                {
                    ordersDb = ordersDb.Where(o => ordersAfterSearch.Where(or => or.OrderStatus == OrderStatus.Completed).Any(p => p.Id == o.Id)).ToList();
                    await PurchaseOrderRepository.DeleteRange(ordersDb);
                    await GetOrders();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete completed orders error:" + ex.Message);
                }
            }
        }
    }
}
