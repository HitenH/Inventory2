using AutoMapper;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

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
        [Inject] private AppDbContext context { get; set; }

        private CustomerModel customerModel = new CustomerModel();
        private CustomerEntity customerEntity;
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;

        protected override async Task OnInitializedAsync()
        {
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
            if (customerModel.Mobiles.Count < 3)
            {
                var count = 3 - customerModel.Mobiles.Count;
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
                    await CustomerRepository.Delete(customerEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete Customer error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/customers");
        }
        public void Dispose()
        {
            if (editContext != null)
                editContext.OnValidationStateChanged -= HandleValidationRequested;
        }
    }
}
