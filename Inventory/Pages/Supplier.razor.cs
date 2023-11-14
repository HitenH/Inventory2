using AutoMapper;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Inventory.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Inventory.Pages
{
    public partial class Supplier
    {
        [Parameter] public string SupplierId { get; set; }

        [Inject] private ISupplierRepository SupplierRepository { get; set; }
        [Inject] private ILogger<Supplier> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IMobileService MobileService { get; set; }
        [Inject] private IConfiguration Config { get; set; }
        [Inject] private AppDbContext context { get; set; }


        private SupplierModel supplierModel = new();
        private SupplierEntity supplierEntity = new();
        private int NumberPhone;
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;

        private ModalWindow? modalWindowComponenRef;
        private string titleMessage = string.Empty;
        private string errorMessageShort = string.Empty;
        private string errorMessageFull = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            NumberPhone = Config.GetSection("SupplierNumberMobile").Value != null ? int.Parse(Config.GetSection("SupplierNumberMobile").Value) : 0;
            
            editContext = new(supplierModel);
            messageStore = new(editContext);
            editContext.OnValidationStateChanged += HandleValidationRequested;

            if (SupplierId != null)
            {
                try
                {
                    supplierEntity = await SupplierRepository.GetById(Guid.Parse(SupplierId));
                    if (supplierEntity == null)
                        navManager.NavigateTo("/suppliers");

                    supplierModel = Mapper.Map<SupplierModel>(supplierEntity);

                }
                catch (Exception ex)
                {
                    Logger.LogError("GetSupplier error: " + ex.Message);
                }
            }
            if (supplierModel.Mobiles.Count < NumberPhone)
            {
                var count = NumberPhone - supplierModel.Mobiles.Count;
                for (int i = 0; i < count; i++)
                {
                    supplierModel.Mobiles.Add(new Mobile() { Phone = "" });
                }
            }
        }

        private void HandleValidationRequested(object? sender, ValidationStateChangedEventArgs args)
        {
            messageStore?.Clear();

            if (String.IsNullOrEmpty(supplierModel.Name))
                messageStore?.Add(() => supplierModel.Name!, "The Supplier name is required!");

            if (String.IsNullOrEmpty(supplierModel.SupplierId))
                messageStore?.Add(() => supplierModel.SupplierId!, "The SupplierID is required!");
            else
            {
                var isExistSupplierId = false;
                if (supplierModel.Id == Guid.Empty)
                    isExistSupplierId = SupplierRepository.IsSupplierIdExist(supplierModel.SupplierId);
                else
                    isExistSupplierId = SupplierRepository.IsSupplierIdExist(supplierModel.SupplierId, supplierModel.Id);

                if (isExistSupplierId)
                    messageStore?.Add(() => supplierModel.SupplierId!, "The SupplierID exists in the database!");
            }
        }

        public async Task AddSupplier()
        {
            if (editContext != null && editContext.Validate())
            {
                if (supplierModel != null)
                {
                    try
                    {
                        supplierEntity = Mapper.Map<SupplierEntity>(supplierModel);
                        var numbers = MobileService.GetMobiles(supplierEntity.Mobiles);
                        supplierEntity.Mobiles = new();

                        if (numbers != null)
                            supplierEntity.Mobiles.AddRange(numbers);

                        await SupplierRepository.Create(supplierEntity);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Create supplier error: " + ex.Message);
                    }
                }
                navManager.NavigateTo("/suppliers");
            }  
        }

        public async Task UpdateSupplier()
        {
            if (editContext != null && editContext.Validate())
            {
                if (supplierModel != null)
                {
                    try
                    {
                        supplierEntity.ContactPerson = supplierModel.ContactPerson;
                        supplierEntity.Name = supplierModel.Name;
                        supplierEntity.SupplierId = supplierModel.SupplierId;
                        supplierEntity.Address = supplierModel.Address;
                        supplierEntity.Area = supplierModel.Area;
                        supplierEntity.Remarks = supplierModel.Remarks;

                        var numbers = MobileService.GetMobiles(supplierModel.Mobiles);
                        supplierEntity.Mobiles = new();
                        supplierEntity.Mobiles.AddRange(numbers);

                        await SupplierRepository.Update(supplierEntity);
                        await MobileService.DeleteEmptyNumbers(context);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Update Supplier error: " + ex.Message);
                    }
                }
                navManager.NavigateTo("/suppliers");
            }   
        }

        public async void DeleteSupplier()
        {
            if (supplierEntity != null)
            {
                try
                {
                    await SupplierRepository.Delete(supplierEntity);
                    navManager.NavigateTo("/suppliers");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete Supplier error: " + ex.Message);
                    titleMessage = "Error Message";
                    errorMessageShort = "Cannot delete Supplier";
                    errorMessageFull = ex.Message;

                    if (modalWindowComponenRef != null)
                        await modalWindowComponenRef.OnShowModalClick();
                }
            }
            
        }

        public void Dispose()
        {
            if (editContext != null)
                editContext.OnValidationStateChanged -= HandleValidationRequested;
        }

        public void CloseModalWindow(bool isClosed)
        {
            if (isClosed)
                ClearErrorMessage();
        }

        private void ClearErrorMessage()
        {
            titleMessage = string.Empty;
            errorMessageShort = string.Empty;
            errorMessageFull = string.Empty;
        }
    }
}
