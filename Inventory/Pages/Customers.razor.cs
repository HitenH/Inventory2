using AutoMapper;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class Customers
    {
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Customers> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private List<CustomerModel> customers = new();
        private List<CustomerModel> customersAfterSearch = new();
        private bool isSortAscending = false;
        private Dictionary<Guid, decimal> totalAmount = new();

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
                    GetTotalAmount();
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
                else if (column == "Mobile")
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
                else if (column == "Area")
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
                else if (column == "Amount")
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

        public void GetTotalAmount()
        {
            if (customersAfterSearch.Count != 0)
            {
                totalAmount = customersAfterSearch.Select(customer => new
                {
                    CustomerId = customer.Id,
                    TotalAmount = customer.Sales.Sum(sale => sale.TotalAmountProduct)
                }).ToDictionary(result => result.CustomerId, result => result.TotalAmount);
            }
        }
    }
}
