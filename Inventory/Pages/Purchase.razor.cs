
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

public partial class Purchase

{

    [Parameter] public string PurchaseId { get; set; }

    [Inject] private IPurchaseRepository PurchaseRepository { get; set; }

    [Inject] private ISupplierRepository SupplierRepository { get; set; }

    [Inject] private ILogger<Purchase> Logger { get; set; }

    [Inject] private NavigationManager navManager { get; set; }

    [Inject] private IDialogService DialogService { get; set; }

    [Inject] private ISnackbar Snackbar { get; set; }

    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;

    // Enter on a chosen supplier (owner's request): a new purchase is saved at once, which moves the cursor to the
    // line's product box (FocusOnOpen); on a saved purchase the cursor just moves there.
    private async Task OnSupplierEnter(SupplierModel _)
    {
        if (PurchaseId == null)
        {
            await AddPurchase();
        }
        else
        {
            await JS.FocusBoxAsync(".focus-line-product");
        }
    }




    private SupplierEntity supplier = new();

    private SupplierModel? selectedSupplier;

    private PurchaseModel purchaseModel = new();

    private PurchaseEntity purchaseEntity = new();

    private ComponentTotalData PurchaseTotalData = new();

    private bool IsDisabled { get; set; }

    // The voucher discount is saved as soon as it is typed; reloading waits for that save so it cannot show the old value.

    private readonly SemaphoreSlim discountSaveLock = new(1, 1);



    protected async override Task OnInitializedAsync()

    {

        if (PurchaseId != null)

        {

            await ReloadPurchaseAsync();

            IsDisabled = purchaseEntity.Id == Guid.Empty;

        }

        else

        {

            IsDisabled = true;

        }

    }



    private async Task ReloadPurchaseAsync()

    {

        await discountSaveLock.WaitAsync();

        try

        {

            purchaseEntity = await PurchaseRepository.GetById(Guid.Parse(PurchaseId!));

        }

        finally

        {

            discountSaveLock.Release();

        }

        if (purchaseEntity.Id == Guid.Empty)

        {

            return;

        }



        purchaseModel.SupplierId = purchaseEntity.Supplier?.SupplierId ?? "";

        purchaseModel.SupplierName = purchaseEntity.Supplier?.Name ?? "";

        purchaseModel.Date = purchaseEntity.Date;

        purchaseModel.Remarks = purchaseEntity.Remarks;

        purchaseModel.TotalAmountProduct = purchaseEntity.TotalAmountProduct;

        purchaseModel.VoucherId = purchaseEntity.VoucherId;

        purchaseModel.Id = purchaseEntity.Id;



        supplier = purchaseEntity.Supplier ?? new SupplierEntity();

        selectedSupplier = supplier.ToModel();



        PurchaseTotalData.Discount = purchaseEntity.Discount.GetValueOrDefault(0);

        GetTotalAmount();

    }



    public async Task AddPurchase()

    {

        if (selectedSupplier is null || supplier.Id == Guid.Empty)

        {

            Snackbar.Add("Please select a supplier.", Severity.Warning);

            return;

        }



        try

        {

            var voucherIdDb = await PurchaseRepository.GetLastVoucherIdByDate(purchaseModel.Date);

            purchaseModel.VoucherId = voucherIdDb == 0 ? 1 : voucherIdDb + 1;



            purchaseEntity = new()

            {

                Date = purchaseModel.Date,

                Remarks = purchaseModel.Remarks,

                SupplierEntityId = supplier.Id,

                VoucherId = purchaseModel.VoucherId,

            };



            var id = await PurchaseRepository.Create(purchaseEntity);

            PurchaseId = id.ToString();

            purchaseModel.Id = id;

            await ReloadPurchaseAsync();

            IsDisabled = false;

        }

        catch (Exception ex)

        {

            Logger.LogError(ex, "Create purchase failed");

            Snackbar.Add($"Saving error: {ex.Message}", Severity.Error);

        }

    }



    public async Task EditPurchase()

    {

        if (purchaseEntity.Id == Guid.Empty || supplier.Id == Guid.Empty)

        {

            Snackbar.Add("Please select a supplier.", Severity.Warning);

            return;

        }



        try

        {

            var isVoucherExist = false;

            if (purchaseEntity.VoucherId != purchaseModel.VoucherId)

            {

                isVoucherExist = await PurchaseRepository.IsVoucherExistByDate(purchaseModel.VoucherId, purchaseModel.Date);

            }



            purchaseEntity.VoucherId = isVoucherExist ? purchaseEntity.VoucherId : purchaseModel.VoucherId;

            if (isVoucherExist)

            {

                purchaseModel.VoucherId = purchaseEntity.VoucherId;

            }



            purchaseEntity.SupplierEntityId = supplier.Id;

            purchaseEntity.Remarks = purchaseModel.Remarks;

            purchaseEntity.Date = purchaseModel.Date;

            purchaseEntity.Discount = PurchaseTotalData.Discount;



            await PurchaseRepository.Update(purchaseEntity);

            await ReloadPurchaseAsync();

            Snackbar.Add("Purchase updated.", Severity.Success);

        }

        catch (Exception ex)

        {

            Logger.LogError(ex, "Update purchase failed");

            Snackbar.Add($"Update error: {ex.Message}", Severity.Error);

        }

    }



    public async Task DeletePurchase()

    {

        if (purchaseEntity.Id == Guid.Empty)

        {

            return;

        }



        try

        {

            if (await DialogService.ConfirmDeleteAsync())

            {

                await PurchaseRepository.Delete(purchaseEntity);

                Snackbar.Add("Purchase deleted", Severity.Success);

                navManager.NavigateTo("/purchases");

            }

        }

        catch (Exception ex)

        {

            Logger.LogError(ex, "Delete purchase failed");

            Snackbar.Add("Something went wrong", Severity.Warning);

        }

    }



    public async Task AddTotalAmount()

    {

        try

        {

            purchaseEntity.TotalAmountProduct = PurchaseTotalData.TotalAmount;

            purchaseEntity.Discount = PurchaseTotalData.Discount;

            await PurchaseRepository.Update(purchaseEntity);

            navManager.NavigateTo("/purchases");

        }

        catch (Exception ex)

        {

            Logger.LogError(ex, "Add total amount failed");

            Snackbar.Add($"Could not save totals: {ex.Message}", Severity.Error);

        }

    }



    private async Task DiscountChanged(decimal value)

    {

        PurchaseTotalData.Discount = value;

        GetTotalAmount();

        if (purchaseEntity.Id == Guid.Empty)

        {

            return;

        }



        // Save straight away, so adding/editing a line or pressing Update no longer resets it.

        await discountSaveLock.WaitAsync();

        try

        {

            // Always save what the box shows now (the latest value, even if several changes came in quickly).

            await PurchaseRepository.UpdateDiscount(purchaseEntity.Id, PurchaseTotalData.Discount);

            purchaseEntity.Discount = PurchaseTotalData.Discount;

        }

        catch (Exception ex)

        {

            Logger.LogError(ex, "Save purchase discount failed");

            Snackbar.Add($"Could not save the discount: {ex.Message}", Severity.Error);

        }

        finally

        {

            discountSaveLock.Release();

        }

    }



    public void GetTotalAmount()

    {

        var lines = purchaseEntity.PurchaseVariants ?? new List<PurchaseVariant>();

        PurchaseTotalData.TotalQuantity = lines.Select(x => x.Quantity).Sum() ?? 0;

        var amount = lines.Select(v => v.AmountAfterDiscount).Sum() ?? 0;

        PurchaseTotalData.TotalAmount = amount - PurchaseTotalData.Discount;

    }



    // Batch 17: the totals bar's Bulk Entry button opens the pop-up of this purchase's line editor.
    private Inventory.Shared.PurchaseVariantComponent? purchaseLines;

    public async Task ChangeStatePurchaseVariant(bool change)

    {

        if (change && PurchaseId != null)

        {

            await ReloadPurchaseAsync();

            StateHasChanged();

        }

    }



    private async Task OnSupplierSelected(SupplierModel? _supplier)

    {

        if (_supplier != null)

        {

            selectedSupplier = _supplier;

            supplier = await SupplierRepository.GetById(_supplier.Id);

            purchaseModel.SupplierId = _supplier.SupplierId ?? "";

            purchaseModel.SupplierName = _supplier.Name ?? "";



            if (purchaseEntity.Id != Guid.Empty)

            {

                purchaseEntity.SupplierEntityId = supplier.Id;

            }

        }

        else

        {

            selectedSupplier = null;

            supplier = new();

            purchaseModel.SupplierId = "";

            purchaseModel.SupplierName = "";

        }

    }

}


