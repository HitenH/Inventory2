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
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private List<CustomerModel> customers;

        protected async override Task OnInitializedAsync()
        {
            customers = new();
            try
            {
                var list = await CustomerRepository.GetAll();
                if (list.Count != 0)
                {
                    customers = list.Select(c => Mapper.Map<CustomerModel>(c)).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Customers page error" + ex.Message);
            }
        }

        private async Task<GridDataProviderResult<CustomerModel>> CustomerDataProvider(GridDataProviderRequest<CustomerModel> request)
        {
            return await Task.FromResult(request.ApplyTo(customers));
        }
    }
}
