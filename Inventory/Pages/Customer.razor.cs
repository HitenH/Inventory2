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
        [Inject] private IMobileService MobileService { get; set; }
        [Inject] private IConfiguration Config { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        [Inject] ISnackbar Snackbar { get; set; }

        private CustomerModel customerModel = new CustomerModel();
        private int NumberPhone;
        private CustomerEntity customerEntity;
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;


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
             
                    customerModel = customerEntity.ToModel();

                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "GetCustomer error");
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

            // Spaces around an ID or name are removed; an ID or name of only spaces counts as empty (batch 5).
            customerModel.CustomerId = customerModel.CustomerId?.Trim()!;
            customerModel.Name = customerModel.Name?.Trim()!;
            
            if (String.IsNullOrWhiteSpace(customerModel.Name))
                messageStore?.Add(() => customerModel.Name!, "The Customer name is required!");

            if (String.IsNullOrWhiteSpace(customerModel.CustomerId))
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
                        customerEntity = customerModel.ToEntity();
                        var numbers = MobileService.GetMobiles(customerEntity.Mobiles);
                        customerEntity.Mobiles = new();

                        if (numbers != null)
                            customerEntity.Mobiles.AddRange(numbers);

                        await CustomerRepository.Create(customerEntity);
                    }
                    catch (Exception ex)
                    {
                        // Stay on the page so the operator can correct the entry (batch 5).
                        Logger.LogError(ex, "Save customer failed");
                        Snackbar.Add(DbErrors.SaveMessage(ex, "customer", "Customer ID"), Severity.Error);
                        return;
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
                    }
                    catch (Exception ex)
                    {
                        // Stay on the page so the operator can correct the entry (batch 5).
                        Logger.LogError(ex, "Save customer failed");
                        Snackbar.Add(DbErrors.SaveMessage(ex, "customer", "Customer ID"), Severity.Error);
                        return;
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
                    if (await DialogService.ConfirmDeleteAsync())
                    {
                        await CustomerRepository.Delete(customerEntity);
                        navManager.NavigateTo("/customers");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Delete Customer error");
                    await DialogService.ShowErrorDetailsAsync("Error Message", "Cannot delete Customer", ex.Message);
                }
            }
        }
        public void Dispose()
        {
            if (editContext != null)
                editContext.OnValidationStateChanged -= HandleValidationRequested;
        }
    }
}
