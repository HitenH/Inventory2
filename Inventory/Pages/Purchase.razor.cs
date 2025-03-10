﻿using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;

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
    [Inject] private IMapper Mapper { get; set; }

    private SupplierEntity supplier = new();
    private SupplierModel selectedSupplier;
    private PurchaseModel purchaseModel = new();
    private PurchaseEntity purchaseEntity = new();
    private ComponentTotalData PurchaseTotalData = new();
    private bool IsDisabled { get; set; }

    protected async override Task OnInitializedAsync()
    {
        if (PurchaseId != null)
        {
            purchaseEntity = await PurchaseRepository.GetById(Guid.Parse(PurchaseId));
            if (purchaseEntity != null)
            {
                purchaseModel.SupplierId = purchaseEntity.Supplier.SupplierId;
                purchaseModel.SupplierName = purchaseEntity.Supplier.Name;
                purchaseModel.Date = purchaseEntity.Date;
                purchaseModel.Remarks = purchaseEntity.Remarks;
                purchaseModel.TotalAmountProduct = purchaseEntity.TotalAmountProduct;
                purchaseModel.VoucherId = purchaseEntity.VoucherId;

                supplier = purchaseEntity.Supplier;
                selectedSupplier = Mapper.Map<SupplierModel>(supplier);

                PurchaseTotalData.Discount = purchaseEntity.Discount.GetValueOrDefault(0);
                IsDisabled = false;
                GetTotalAmount();
            } 
        }
        else
            IsDisabled = true;
    }
    public async Task AddPurchase()
    {
        if (selectedSupplier != null)
        {
            try
            {
                var voucherIdDb = await PurchaseRepository.GetLastVoucherIdByDate(purchaseModel.Date);
                if (voucherIdDb == 0)
                    purchaseModel.VoucherId = 1;
                else
                    purchaseModel.VoucherId = voucherIdDb + 1;

                purchaseEntity = new()
                {
                    Date = purchaseModel.Date,
                    Remarks = purchaseModel.Remarks,
                    Supplier = supplier,
                    VoucherId = purchaseModel.VoucherId,
                };
                var id = await PurchaseRepository.Create(purchaseEntity);
                PurchaseId = id.ToString();
                purchaseModel.Id = id;
                purchaseEntity = await PurchaseRepository.GetById(id);
                IsDisabled = false;
            }
            catch (Exception ex)
            {
                //Snackbar message
                Snackbar.Add($"Saving Error: {ex.Message}", Severity.Error);
            }
        }
        else
        {
            Snackbar.Add("There is data missing.", Severity.Error);
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
                    isVoucherExist = await PurchaseRepository.IsVoucherExistByDate(purchaseModel.VoucherId, purchaseModel.Date);

                if (!isVoucherExist)
                    purchaseEntity.VoucherId = purchaseModel.VoucherId;
                else
                    purchaseModel.VoucherId = purchaseEntity.VoucherId;

                if (purchaseEntity.Supplier.Id != supplier.Id)
                    purchaseEntity.Supplier = supplier;

                purchaseEntity.Remarks = purchaseModel.Remarks;
                purchaseEntity.Date = purchaseModel.Date;

                await PurchaseRepository.Update(purchaseEntity);
            }
            catch (Exception ex)
            {
                //Snackbar message
                Snackbar.Add($"Update Error: {ex.Message}", Severity.Error);
            }
        }
    }
    public async Task DeletePurchase()
    {
        if (purchaseEntity != null)
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
                    await PurchaseRepository.Delete(purchaseEntity);
                    Snackbar.Add("Purchase deleted", Severity.Success);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add("Something went wrong", Severity.Warning);
            }
            navManager.NavigateTo("/purchases");
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
            Logger.LogError("Add total amount: " + ex.Message);
        }
    }
    private void DiscountChanged(decimal value)
    {
        PurchaseTotalData.Discount = value;
        GetTotalAmount();
    }

    public void GetTotalAmount()
    {
        PurchaseTotalData.TotalQuantity = purchaseEntity.PurchaseVariants.Select(x => x.Quantity).Sum().Value;
        var amount = purchaseEntity.PurchaseVariants.Select(v => v.AmountAfterDiscount).Sum().Value;
        PurchaseTotalData.TotalAmount = amount - PurchaseTotalData.Discount;
    }

    public void ChangeStatePurchaseVariant(bool change)
    {
        if (change)
            GetTotalAmount();
    }

    //On Supplier selected
    private async Task OnSupplierSelected(SupplierModel? _supplier)
    {
        if (_supplier != null)
        {
            selectedSupplier = _supplier;

            //Set supplier entity to _supplier that is selected

            supplier = await SupplierRepository.GetById(_supplier.Id);


            purchaseModel.SupplierId = _supplier.SupplierId;
            purchaseModel.SupplierName = _supplier.Name;
        }
        else
        {
            selectedSupplier = new();
            supplier = new();
            purchaseModel.SupplierId = "";
            purchaseModel.SupplierName = "";
        }
    }
}
