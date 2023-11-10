using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class Sales
    {
        [Inject] private ISaleRepository SaleRepository { get; set; }
        [Inject] private ILogger<Sales> Logger { get; set; }

        private List<SalesModel> sales = new();
        private List<SalesModel> salesAfterSearch = new();
        private bool isSortAscending = false;

        protected async override Task OnInitializedAsync()
        {
            try
            {
                var list = await SaleRepository.GetAll();
                if (list.Count != 0)
                {
                    sales = list.Select(c =>
                     new SalesModel()
                     {
                         CustomerId = c.Customer.CustomerId,
                         CustomerName = c.Customer.Name,
                         Date = c.Date,
                         VoucherId = c.VoucherId,
                         Id= c.Id
                     }
                    ).ToList();
                    salesAfterSearch = sales;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Sales error" + ex.Message);
            }
        }

        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            salesAfterSearch = sales.Where(n => n.VoucherId.ToString().Contains(search) || n.CustomerName.ToLower().Contains(search)
                                                        || n.Date.ToString().Contains(search)).ToList();
        }

        public void SortItem(string column)
        {
            if (salesAfterSearch.Count != 0)
            {
                if (column == "VoucherId")
                {
                    if (isSortAscending)
                    {
                        salesAfterSearch = salesAfterSearch.OrderBy(c => c.VoucherId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesAfterSearch = salesAfterSearch.OrderByDescending(c => c.VoucherId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "CustomerName")
                {
                    if (isSortAscending)
                    {
                        salesAfterSearch = salesAfterSearch.OrderBy(c => c.CustomerName).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesAfterSearch = salesAfterSearch.OrderByDescending(c => c.CustomerName).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Date")
                {
                    if (isSortAscending)
                    {
                        salesAfterSearch = salesAfterSearch.OrderBy(c => c.Date).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesAfterSearch = salesAfterSearch.OrderByDescending(c => c.Date).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }
}
