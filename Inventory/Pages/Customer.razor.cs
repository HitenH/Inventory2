using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;


namespace Inventory.Pages
{
    public partial class Customer
    {
        [Parameter] public string CustomerId { get; set; }

        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private CustomerModel customerModel = new CustomerModel();
        private CustomerEntity customerEntity;

        protected override async Task OnInitializedAsync()
        {
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
            if (customerModel.Numbers.Count < 3)
            {
                var count = 3 - customerModel.Numbers.Count;
                for (int i = 0; i < count; i++)
                {
                    customerModel.Numbers.Add(new Number() { Phone = "" });
                }
            }
        }

        public async Task AddCustomer()
        {
            if (customerModel != null)
            {
                try
                {
                    customerEntity = Mapper.Map<CustomerEntity>(customerModel);
                    var numbers = GetNumbers(customerEntity.Numbers);
                    customerEntity.Numbers = new();

                    if (numbers != null)
                        customerEntity.Numbers.AddRange(numbers);

                    await CustomerRepository.Create(customerEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Create Customer error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/customers");
        }

        public async Task UpdateCustomer()
        {
            if (customerModel != null)
            {
                try
                {
                    customerEntity.ContactPerson = customerModel.ContactPerson;
                    customerEntity.Name = customerModel.Name;
                    customerEntity.Address = customerModel.Address;
                    customerEntity.Area = customerModel.Area;
                    customerEntity.Numbers = customerModel.Numbers;
                    customerEntity.Remarks = customerModel.Remarks;

                    var numbers = GetNumbers(customerEntity.Numbers);
                    customerEntity.Numbers = new();

                    if (numbers != null)
                        customerEntity.Numbers.AddRange(numbers);

                    await CustomerRepository.Update(customerEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update Customer error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/customers");
        }

        public async void DeleteCustomer()
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

        private List<Number> GetNumbers(List<Number> list)
        {
            var numbers = new List<Number>();
            foreach (var item in list)
            {
                if (item.Phone != "")
                    numbers.Add(item);
            }
            return numbers;
        }
    }
}
