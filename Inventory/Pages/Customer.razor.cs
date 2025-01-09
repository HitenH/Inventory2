using AutoMapper;
using BlazorBootstrap;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Inventory.Service;
using Inventory.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Inventory.Pages
{
    public partial class Customer
    {
        [Parameter] public string CustomerId { get; set; }
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Customer> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IMobileService MobileService { get; set; }
        [Inject] private IConfiguration Config { get; set; }
        [Inject] private AppDbContext context { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        [Inject] ISnackbar Snackbar { get; set; }

        private CustomerModel customerModel = new CustomerModel();
        private int NumberPhone;
        private CustomerEntity customerEntity;
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;

        private ModalWindow? modalWindowComponenRef;
        private string titleMessage = string.Empty;
        private string errorMessageShort = string.Empty;
        private string errorMessageFull = string.Empty;

        protected override async Task OnInitializedAsync()
        {
            NumberPhone = Config.GetSection("CustomerNumberMobile").Value != null? int.Parse(Config.GetSection("CustomerNumberMobile").Value) : 0;

            editContext = new(customerModel);
            messageStore = new(editContext);
            editContext.OnValidationStateChanged += HandleValidationRequested;

            if (CustomerId != null)
            {
                try
                {
                    customerEntity = await CustomerRepository.GetById(Guid.Parse(CustomerId));
                    if (customerEntity == null)
                        navManager.NavigateTo("/customers");
             
                    customerModel = Mapper.Map<CustomerModel>(customerEntity);

                }
                catch (Exception ex)
                {
                    Logger.LogError("GetCustomer error: " + ex.Message);
                }
            }
            if (customerModel.Mobiles.Count < NumberPhone)
            {
                var count = NumberPhone - customerModel.Mobiles.Count;
                for (int i = 0; i < count; i++)
                {
                    customerModel.Mobiles.Add(new Mobile() { Phone = "" });
                }
            }
        }
        private void HandleValidationRequested(object? sender, ValidationStateChangedEventArgs args)
        {
            messageStore?.Clear();
            
            if (String.IsNullOrEmpty(customerModel.Name))
                messageStore?.Add(() => customerModel.Name!, "The Customer name is required!");

            if (String.IsNullOrEmpty(customerModel.CustomerId))
                messageStore?.Add(() => customerModel.CustomerId!, "The CustomerID is required!");
            else
            {
                var isExistCustomerId = false;
                if (customerModel.Id == Guid.Empty)
                    isExistCustomerId = CustomerRepository.IsCustomIdExist(customerModel.CustomerId);
                else
                    isExistCustomerId = CustomerRepository.IsCustomIdExist(customerModel.CustomerId, customerModel.Id);

                if (isExistCustomerId)
                    messageStore?.Add(() => customerModel.CustomerId!, "The CustomerID exists in the database!");
            } 
        }
        public async Task AddCustomer()
        {
            if (editContext != null && editContext.Validate())
            {
                if (customerModel != null)
                {
                    try
                    {
                        customerEntity = Mapper.Map<CustomerEntity>(customerModel);
                        var numbers = MobileService.GetMobiles(customerEntity.Mobiles);
                        customerEntity.Mobiles = new();

                        if (numbers != null)
                            customerEntity.Mobiles.AddRange(numbers);

                        await CustomerRepository.Create(customerEntity);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Create Customer error: " + ex.Message);
                    }
                }
                navManager.NavigateTo("/customers");
            }
        }

        public async Task UpdateCustomer()
        {
            if (editContext != null && editContext.Validate())
            {
                if (customerModel != null)
                {
                    try
                    {
                        customerEntity.ContactPerson = customerModel.ContactPerson;
                        customerEntity.CustomerId = customerModel.CustomerId;
                        customerEntity.Name = customerModel.Name;
                        customerEntity.Address = customerModel.Address;
                        customerEntity.Area = customerModel.Area;
                        customerEntity.Remarks = customerModel.Remarks;

                        var numbers = MobileService.GetMobiles(customerModel.Mobiles);
                        customerEntity.Mobiles = new();
                        customerEntity.Mobiles.AddRange(numbers);


                        await CustomerRepository.Update(customerEntity);
                        await MobileService.DeleteEmptyNumbers(context);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Update Customer error: " + ex.Message);
                    }
                }
                navManager.NavigateTo("/customers");
            }
                
        }

        public async Task DeleteCustomer()
        {
            if (customerEntity != null)
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
                        await CustomerRepository.Delete(customerEntity);
                        navManager.NavigateTo("/customers");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete Customer error: " + ex.Message);
                    titleMessage = "Error Message";
                    errorMessageShort = "Cannot delete Customer";
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
            titleMessage= string.Empty;
            errorMessageShort = string.Empty;
            errorMessageFull = string.Empty;
        }
    }
}
