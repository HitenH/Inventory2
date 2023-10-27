using AutoMapper;
using BlazorBootstrap;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;


namespace Inventory.Pages
{
    public partial class Customers
    {
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private List<CustomerModel> customers = new();
        private List<CustomerModel> customersAfterSearch = new();

        protected async override Task OnInitializedAsync()
        {
            customers = new();
            try
            {
                var list = await CustomerRepository.GetAll();
                if (list.Count != 0)
                {
                    customers = list.Select(c => Mapper.Map<CustomerModel>(c)).ToList();
                    customersAfterSearch = customers;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Customers page error" + ex.Message);
            }
        }
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            customersAfterSearch = customers.Where(n => n.CustomerId.ToLower().Contains(search) || n.Name.ToLower().Contains(search)).ToList();
        }
    }
}
