using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages
{
    public partial class PurchaseOrder
    {
        [Inject] private IPurchaseOrderRepository PurchaseOrderRepository { get; set; }
        [Inject] private ILogger<PurchaseOrder> Logger { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        [Inject] ISupplierRepository SupplierRepository { get; set; }
        [Inject] IProductRepository ProductRepository { get; set; }
        [Inject] ISnackbar Snackbar { get; set; }
        [Inject] IMapper Mapper { get; set; }
        //Get enums list to populate order status
        private List<OrderStatus> orderStatuses = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().ToList();

        private PurchaseOrderEntity purchaseOrderEntity = new();
        private PurchaseOrderModel purchaseOrderModel = new();
        private PurchaseOrderModel purchaseOrderModelBackup = new();
        private List<PurchaseOrderModel> orders = new();
        private List<PurchaseOrderModel> ordersAfterSearch = new();
        private HashSet<PurchaseOrderModel> selectedOrders = new();
        private List<SupplierModel> suppliers = new();
        private List<SupplierModel> suppliersAfterSearch = new();
        private List<ProductModel> products = new();
        private List<ProductModel> productsAfterSearch = new();
        private ProductModel selectedProduct = new();
        private SupplierModel selectedSupplier = new();
        private List<PurchaseOrderEntity> ordersDb = new();
        private Snackbar snackbar;


        protected async override Task OnInitializedAsync()
        {
            await GetOrders();
            await GetSuppliersAsync();
            await GetProductsAsync();
        }

        public async Task GetOrders()
        {
            try
            {
                ordersDb = await PurchaseOrderRepository.GetAll();
                orders = ordersDb.Select(o => new PurchaseOrderModel()
                {
                    Date = o.Date,
                    DueDate = o.DueDate,
                    Id = o.Id,
                    OrderStatus = o.OrderStatus,
                    ProductId = o.ProductId,
                    ProductName = o.ProductName,
                    ProductRate = o.ProductRate,
                    Quantity = o.Quantity,
                    Remarks = o.Remarks,
                    SupplierId = o.SupplierId,
                    VariantId = o.VariantId
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
                    purchaseOrderEntity.ProductId = selectedProduct.ProductId;
                    purchaseOrderEntity.ProductName = selectedProduct.Name;
                    purchaseOrderEntity.OrderStatus = purchaseOrderModel.OrderStatus;
                    purchaseOrderEntity.Date = purchaseOrderModel.Date;
                    purchaseOrderEntity.DueDate = purchaseOrderModel.DueDate;
                    purchaseOrderEntity.Remarks = purchaseOrderModel.Remarks;
                    purchaseOrderEntity.ProductRate = purchaseOrderModel.ProductRate;
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
                        purchaseOrderEntity.ProductId = selectedProduct.ProductId;
                        purchaseOrderEntity.ProductName = selectedProduct.Name;
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
            purchaseOrderEntity = new();
            selectedProduct = new();
            selectedSupplier = new();
        }

        public async Task OpenSupplierDialogAsync()
        {
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Large,
                CloseOnEscapeKey = true,
                CloseButton = true,
                Position = DialogPosition.Center
            };
            var dialog = await DialogService.ShowAsync<SuppliersDialog>("Suppliers List", options);
            var result = await dialog.Result;
            if (!result.Canceled)
            {
                var supplierModel = (SupplierModel)result.Data;
                OnSupplierSelected(supplierModel);
            }
        }

        public async Task OpenProductDialogAsync()
        {
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Large,
                CloseOnEscapeKey = true,
                CloseButton = true,
                Position = DialogPosition.Center
            };
            var dialog = await DialogService.ShowAsync<ProductsDialog>("Products List", options);
            var result = await dialog.Result;

            if (!result.Canceled)
            {
                selectedProduct = (ProductModel)result.Data;
                OnProductSelected(selectedProduct);
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

        public async Task DeleteSelectedOrders()
        {
            if (ordersAfterSearch.Count > 0)
            {
                try
                {
                    var parameters = new DialogParameters<ConfirmationDialog>
                {
                    { x => x.ContentText, "Do you really want to delete these records? This process cannot be undone." },
                    { x => x.ButtonText, "Delete" },
                    { x => x.Color, Color.Error }
                };

                    var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };

                    var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete", parameters, options);
                    var result = await dialog.Result;
                    if (!result.Canceled)
                    {
                        //delete the selected orders
                        ordersDb = ordersDb.Where(o => selectedOrders.Any(p => p.Id == o.Id)).ToList();
                        await PurchaseOrderRepository.DeleteRange(ordersDb);
                        //Snackbar to tell user what was deleted
                        snackbar = Snackbar.Add($"Deleted records successfully.", Severity.Success);
                        await GetOrders();
                    }
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
                    var parameters = new DialogParameters<ConfirmationDialog>
                {
                    { x => x.ContentText, "Do you really want to delete these records? This process cannot be undone." },
                    { x => x.ButtonText, "Delete" },
                    { x => x.Color, Color.Error }
                };

                    var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };

                    var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete", parameters, options);
                    var result = await dialog.Result;
                    if (!result.Canceled)
                    {
                        ordersDb = ordersDb.Where(o => ordersAfterSearch.Where(or => or.OrderStatus == OrderStatus.Completed).Any(p => p.Id == o.Id)).ToList();
                        await PurchaseOrderRepository.DeleteRange(ordersDb);

                        //Inform user process was successful
                        snackbar = Snackbar.Add($"Deleted completed orders successfully.", Severity.Success);

                        await GetOrders();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete completed orders error:" + ex.Message);
                }
            }
        }


        //For Row Editing
        private void BackupItem(object item)
        {
            purchaseOrderModelBackup = (PurchaseOrderModel)item;
        }
        private void ResetItemToOriginalValues(object item)
        {
            var index = ordersAfterSearch.IndexOf((PurchaseOrderModel)item);
            ordersAfterSearch[index] = purchaseOrderModelBackup;
        }

        public async void EditRowPurchaseOrder(object e)
        {
            PurchaseOrderModel editPurchaseOrderModel = ((PurchaseOrderModel)e);

            if (editPurchaseOrderModel != null)
            {
                try
                {
                    purchaseOrderEntity = await PurchaseOrderRepository.GetById(editPurchaseOrderModel.Id);
                    if (purchaseOrderEntity != null)
                    {
                        purchaseOrderEntity.SupplierId = editPurchaseOrderModel.SupplierId;
                        purchaseOrderEntity.Date = editPurchaseOrderModel.Date;
                        purchaseOrderEntity.ProductId = editPurchaseOrderModel.ProductId;
                        purchaseOrderEntity.ProductName = editPurchaseOrderModel.ProductName;
                        purchaseOrderEntity.Quantity = editPurchaseOrderModel.Quantity;
                        purchaseOrderEntity.ProductRate = editPurchaseOrderModel.ProductRate;
                        purchaseOrderEntity.OrderStatus = editPurchaseOrderModel.OrderStatus;
                        purchaseOrderEntity.VariantId = editPurchaseOrderModel.VariantId;
                        purchaseOrderEntity.Remarks = editPurchaseOrderModel.Remarks;
                        purchaseOrderEntity.DueDate = editPurchaseOrderModel.DueDate;

                        await PurchaseOrderRepository.Update(purchaseOrderEntity);
                        CancelOrder();
                        await GetOrders();

                        snackbar = Snackbar.Add($"Updated {editPurchaseOrderModel.Id} successfully.", Severity.Success);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update order error: " + ex.Message);
                }
            }
        }

        private async Task<IEnumerable<SupplierModel>> SearchSupplierEntities(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value))
                return suppliersAfterSearch.ToList();

            return suppliers.Where(n => n.SupplierId.ToLower().Contains(value) || n.Name.ToLower().Contains(value))
                            .ToList();
        }

        private async Task GetSuppliersAsync()
        {
            try
            {
                var suppliersDb = await SupplierRepository.GetAll();
                if (suppliersDb.Count != 0)
                {
                    suppliers = suppliersDb.Select(s => Mapper.Map<SupplierModel>(s)).ToList();
                    suppliersAfterSearch = suppliers;
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                snackbar = Snackbar.Add("Something went wrong", Severity.Warning);
            }
        }
        //Getting data for products
        private async Task<IEnumerable<ProductModel>> SearchProductEntities(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value))
                return productsAfterSearch.ToList();
            return products.Where(n => n.ProductId.ToLower().Contains(value) || n.Name.ToLower().Contains(value))
                         .ToList();
        }
        //On Supplier selected
        private void OnSupplierSelected(SupplierModel? supplier)
        {
            if (supplier != null)
            {
                selectedSupplier = supplier;
                purchaseOrderModel.SupplierId = supplier.SupplierId;
            }
            else
            {
                selectedSupplier = new();
                purchaseOrderModel.SupplierId = "";
            }
        }
        private void OnProductSelected(ProductModel? product)
        {
            if (product != null)
            {
                selectedProduct = product;
                purchaseOrderModel.ProductId = product.ProductId;
                purchaseOrderModel.ProductName = product.Name;
            }
            else
            {
                selectedProduct = new();
                // Clear the fields if no product is selected
                purchaseOrderModel.ProductId = "";
                purchaseOrderModel.ProductName = "";
            }
        }

        private async Task GetProductsAsync()
        {
            try
            {
                var productsDb = await ProductRepository.GetAll();
                if (productsDb.Count != 0)
                {
                    products = productsDb.Select(p => Mapper.Map<ProductModel>(p)).ToList();
                    productsAfterSearch = products;
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                snackbar = Snackbar.Add("Something went wrong while getting products.", Severity.Error);
            }
        }
    }
}
