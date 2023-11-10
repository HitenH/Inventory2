using AutoMapper;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Pages;
using Microsoft.AspNetCore.Components;

namespace Inventory.Shared
{
    public partial class CustomerPopup
    {
        [Parameter] public bool IsVisible { get; set; }
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<CustomerPopup> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        [Parameter] public EventCallback<bool> CloseCallBack { get; set; }
        [Parameter] public EventCallback<CustomerModel> CustomerCallBack { get; set; }

        private List<CustomerModel> customers = new();
        private List<CustomerModel> customersAfterSearch = new();
        private bool isSortAscending = false;


        protected async override Task OnParametersSetAsync()
        {
            try
            {
                var customersDb = await CustomerRepository.GetAll();
                if (customersDb.Count != 0)
                {
                    customers = customersDb.Select(p => Mapper.Map<CustomerModel>(p)).ToList();
                    customersAfterSearch = customers;
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError("Customer popup error: " + ex.Message);
            }
        }
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            customersAfterSearch = customers.Where(n => n.CustomerId.ToLower().Contains(search) || n.Name.ToLower().Contains(search)).ToList();
        }

        private void Close()
        {
            IsVisible = false;
            CloseCallBack.InvokeAsync(IsVisible);
            StateHasChanged();
        }

        public async Task SelectCustomer(CustomerModel customer)
        {
            await CustomerCallBack.InvokeAsync(customer);
            Close();
        }

        public void SortItem(string column)
        {
            if (customersAfterSearch.Count != 0)
            {
                if (column == "CustomerId")
                {
                    if (isSortAscending)
                    {
                        customersAfterSearch = customersAfterSearch.OrderBy(c => c.CustomerId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        customersAfterSearch = customersAfterSearch.OrderByDescending(c => c.CustomerId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Name")
                {
                    if (isSortAscending)
                    {
                        customersAfterSearch = customersAfterSearch.OrderBy(c => c.Name).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        customersAfterSearch = customersAfterSearch.OrderByDescending(c => c.Name).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }

    }
}
