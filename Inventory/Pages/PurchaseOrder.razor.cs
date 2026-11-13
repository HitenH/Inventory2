using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages;

[Authorize]
public partial class PurchaseOrder
{
    [Inject] private IPurchaseOrderRepository PurchaseOrderRepository { get; set; }
    [Inject] private ILogger<PurchaseOrder> Logger { get; set; }
    [Inject] IDialogService DialogService { get; set; }
    [Inject] ISupplierRepository SupplierRepository { get; set; }
    [Inject] IProductRepository ProductRepository { get; set; }
    [Inject] ISnackbar Snackbar { get; set; }
    //Get enums list to populate order status
    private List<OrderStatus> orderStatuses = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().ToList();

    private PurchaseOrderEntity purchaseOrderEntity = new();
    private PurchaseOrderModel purchaseOrderModel = new();
    private PurchaseOrderModel purchaseOrderModelBackup = new();
    private List<PurchaseOrderModel> orders = new();
    private List<PurchaseOrderModel> ordersAfterSearch = new();
    private HashSet<PurchaseOrderModel> selectedOrders = new();
    // Start empty (not null): saving before a product was picked crashed the screen (seen Nov 2026).
    private ProductModel selectedProduct = new();
    private SupplierModel selectedSupplier = new();
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;

    // Enter on a chosen name moves the cursor on: Supplier -> Product -> Variant (owner's request).
    private Task OnSupplierEnter(SupplierModel _) => JS.FocusBoxAsync(".focus-po-product");
    private Task OnProductEnter(ProductModel _) => JS.FocusBoxAsync("#po-variant");
    private List<PurchaseOrderEntity> ordersDb = new();


    protected async override Task OnInitializedAsync()
    {
        await GetOrders();
    }

    public async Task GetOrders()
    {
        try
        {
            ordersDb = await PurchaseOrderRepository.GetAll();
            // Orders store only the supplier's ID; the list also shows the name (owner's request, Nov 2026).
            var supplierNames = await LoadSupplierNamesAsync();
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
                SupplierName = SupplierNameFor(supplierNames, o.SupplierId),
                VariantId = o.VariantId
            }).ToList();
            //Order by Date latest
            ordersAfterSearch = [.. orders.OrderByDescending(o => o.Date)];
            // The list was rebuilt, so the ticks on screen are gone; forget them here too
            // (an order that no longer looks ticked must not be deleted or changed).
            selectedOrders = new();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Purchase order error");
        }
    }
    public async Task AddPurchaseOrder()
    {
        // Stop here when something is missing (it used to show the message and save anyway, or crash).
        if (string.IsNullOrEmpty(purchaseOrderModel.SupplierId))
        {
            Snackbar.Add("Supplier ID is missing.", Severity.Info);
            return;
        }

        if (string.IsNullOrEmpty(selectedProduct?.Name))
        {
            Snackbar.Add("Please select a product.", Severity.Info);
            return;
        }


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
                // Ready for the next order: cursor back in Supplier ID (owner's request).
                await JS.FocusBoxAsync(".focus-supplier");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Add new order error");
            }
        }
        else
        {
            Snackbar.Add("There is data missing", Severity.Info);
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
                    await JS.FocusBoxAsync(".focus-supplier");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Update order error");
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

    // Batch 14: the list shows one page at a time (see ListPaging).
    private MudBlazor.MudTable<PurchaseOrderModel>? listTable;

    public void SearchItem(ChangeEventArgs e)
    {
        listTable?.NavigateTo(0);   // a new search starts on page 1 (batch 14)
        var search = e.Value.ToString().ToLower();
        ordersAfterSearch = orders.Where(n => (n.SupplierId ?? "").ToLower().Contains(search)
                                           || (n.SupplierName ?? "").ToLower().Contains(search)
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
                if (await DialogService.ConfirmDeleteAsync(selectedOrders.Count))
                {
                    //delete the selected orders
                    ordersDb = ordersDb.Where(o => selectedOrders.Any(p => p.Id == o.Id)).ToList();
                    await PurchaseOrderRepository.DeleteRange(ordersDb);
                    //Snackbar to tell user what was deleted
                    Snackbar.Add($"Deleted records successfully.", Severity.Success);
                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete selected orders error");
            }
        }
    }
    private OrderStatus statusForSelected = OrderStatus.Completed;

    /// <summary>Owner's request (Nov 2026): set the status of every ticked order at once, after one confirmation.</summary>
    private async Task SetStatusOfSelectedAsync()
    {
        if (selectedOrders.Count == 0)
        {
            Snackbar.Add("Tick the orders to change first.", Severity.Info);
            return;
        }
        var count = selectedOrders.Count;
        var text = count == 1
            ? $"Change the status of this 1 order to {statusForSelected}?"
            : $"Change the status of these {count} orders to {statusForSelected}?";
        if (!await DialogService.ConfirmAsync("Change status", text, "Change", Color.Primary, enterConfirms: true))
        {
            return;
        }
        try
        {
            var changed = await PurchaseOrderRepository.UpdateStatus(selectedOrders.Select(o => o.Id).ToList(), statusForSelected);
            Snackbar.Add(changed == 1 ? $"1 order set to {statusForSelected}." : $"{changed} orders set to {statusForSelected}.", Severity.Success);
            selectedOrders = new();
            await GetOrders();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Changing the status of purchase orders failed");
            Snackbar.Add("Could not change the status. Nothing was changed.", Severity.Error);
        }
    }

    public async Task DeleteCompletedOrders()
    {
        if (ordersAfterSearch.Count > 0)
        {
            try
            {
                if (await DialogService.ConfirmDeleteAsync(ordersAfterSearch.Count(o => o.OrderStatus == OrderStatus.Completed)))
                {
                    ordersDb = ordersDb.Where(o => ordersAfterSearch.Where(or => or.OrderStatus == OrderStatus.Completed).Any(p => p.Id == o.Id)).ToList();
                    await PurchaseOrderRepository.DeleteRange(ordersDb);

                    //Inform user process was successful
                    Snackbar.Add($"Deleted completed orders successfully.", Severity.Success);

                    await GetOrders();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete completed orders error");
            }
        }
    }


    //For Row Editing
    private void BackupItem(object item)
    {
        purchaseOrderModelBackup = ((PurchaseOrderModel)item).Clone();
    }
    private void ResetItemToOriginalValues(object item)
    {
        ((PurchaseOrderModel)item).CopyFrom(purchaseOrderModelBackup);
    }

    // MudTable's RowEditCommit only accepts a plain (non-async) method, so this starts the save
    // and the async method below catches every error itself (an async void would crash the page).
    public void EditRowPurchaseOrder(object e) => _ = EditRowPurchaseOrderAsync(e);

    private async Task EditRowPurchaseOrderAsync(object e)
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

                    Snackbar.Add($"Updated {editPurchaseOrderModel.Id} successfully.", Severity.Success);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Update order error");
                Snackbar.Add("Could not save the purchase order row. " + ex.Message, Severity.Error);
            }
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<Dictionary<string, string>> LoadSupplierNamesAsync()
    {
        try
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in await SupplierRepository.GetAllForLookup())
            {
                if (!string.IsNullOrWhiteSpace(s.SupplierId))
                {
                    names.TryAdd(s.SupplierId.Trim(), s.Name ?? "");
                }
            }
            return names;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Purchase orders: loading supplier names failed");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string SupplierNameFor(Dictionary<string, string> names, string? supplierId)
        => supplierId != null && names.TryGetValue(supplierId.Trim(), out var name) ? name : "";

    /// <summary>
    /// Bulk Entry pop-up (owner's request): supplier, dates and status once, then product + variant + quantity rows.
    /// Every row becomes its own purchase order; all are saved together or none.
    /// </summary>
    private async Task OpenBulkEntryAsync()
    {
        var header = new BulkOrderHeader
        {
            SupplierId = purchaseOrderModel.SupplierId,
            Date = purchaseOrderModel.Date,
            DueDate = purchaseOrderModel.DueDate,
            OrderStatus = purchaseOrderModel.OrderStatus,
        };
        var result = await BulkEntryDialog.ShowAsync(DialogService, BulkEntryKind.PurchaseOrder, selectedSupplier, header);
        if (result == null)
        {
            await JS.FocusBoxAsync(".focus-supplier");
            return;
        }
        try
        {
            await PurchaseOrderRepository.CreateRange(BulkEntry.ToPurchaseOrders(result.Rows, result.Header));
            Snackbar.Add(result.Rows.Count == 1 ? "Saved 1 purchase order." : $"Saved {result.Rows.Count} purchase orders.", Severity.Success);
            CancelOrder();
            await GetOrders();
            await JS.FocusBoxAsync(".focus-supplier");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Bulk entry: saving purchase orders failed");
            Snackbar.Add("Could not save the purchase orders. Nothing was saved. " + DbErrors.RuleMessage(ex), Severity.Error);
        }
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
    private async Task OnProductSelected(ProductModel? product)
    {
        if (product != null)
        {
            var productWithVariants = await ProductRepository.GetProductWithVariantsById(product.Id);
            selectedProduct = productWithVariants.ToModel();
            purchaseOrderModel.ProductId = selectedProduct.ProductId;
            purchaseOrderModel.ProductName = selectedProduct.Name;
            purchaseOrderModel.ProductRate ??= selectedProduct.Rate;

            if (selectedProduct.Variants is { Count: > 0 })
            {
                purchaseOrderModel.VariantId = selectedProduct.Variants[0].VariantId;
            }
            else
            {
                purchaseOrderModel.VariantId = null;
                Snackbar.Add("This product has no variants. Add a variant on the Products page first.", Severity.Warning);
            }
        }
        else
        {
            selectedProduct = new();
            purchaseOrderModel.ProductId = "";
            purchaseOrderModel.ProductName = "";
            purchaseOrderModel.VariantId = null;
        }
    }
}
