using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class SalesOrders
    {
        [Inject] public ISalesOrderRepository SalesOrderRepository { get; set; }
        [Inject] public ILogger<SalesOrders> Logger { get; set; }

        private List<SalesOrderModel> salesOrders = new();
        private List<SalesOrderModel> salesOrdersAfterSearch = new();
        private List<SalesOrderEntity> salesOrdersDb = new();
        private bool isSortAscending = false;
        private bool isSelected = false;

        protected async override Task OnInitializedAsync()
        {
            await GetOrders();
        }

        public async Task GetOrders()
        {
            try
            {
                salesOrdersDb = await SalesOrderRepository.GetAll();
                salesOrders = salesOrdersDb.Select(o => new SalesOrderModel()
                {
                    CustomerId = o.Customer.CustomerId,
                    CustomerName = o.Customer.Name,
                    Date = o.Date,
                    DueDate = o.DueDate,
                    Id = o.Id,
                    VoucherId = o.VoucherId,
                    OrderStatus = o.OrderStatus,
                    TotalAmountProduct = o.TotalAmountProduct
                }).ToList();
                salesOrdersAfterSearch = salesOrders;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError("SalesOrders error: " + ex.Message);
            }
        }

        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            salesOrdersAfterSearch = salesOrders.Where(n => n.CustomerName.ToLower().Contains(search)
                                               || n.VoucherId.ToString().Contains(search)
                                               || n.Date.ToString().Contains(search)
                                               || n.DueDate.ToString().Contains(search)
                                               || n.OrderStatus.ToString().ToLower().Contains(search)).ToList();
        }

        public void SortItem(string column)
        {
            if (salesOrdersAfterSearch.Count != 0)
            {
                if (column == "VoucherId")
                {
                    if (isSortAscending)
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderBy(c => c.VoucherId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderByDescending(c => c.VoucherId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "CustomerName")
                {
                    if (isSortAscending)
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderBy(c => c.CustomerName).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderByDescending(c => c.CustomerName).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Date")
                {
                    if (isSortAscending)
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderBy(c => c.Date).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderByDescending(c => c.Date).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "DueDate")
                {
                    if (isSortAscending)
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderBy(c => c.DueDate).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderByDescending(c => c.DueDate).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Satus")
                {
                    if (isSortAscending)
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderBy(c => c.OrderStatus).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        salesOrdersAfterSearch = salesOrdersAfterSearch.OrderByDescending(c => c.OrderStatus).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }

        public void SelectAllItems()
        {
            isSelected = !isSelected;
            if (isSelected)
            {
                foreach (var order in salesOrdersAfterSearch)
                {
                    order.IsChecked = true;
                }
            }
            else
            {
                foreach (var order in salesOrdersAfterSearch)
                {
                    order.IsChecked = false;
                }
            }
        }
        public async Task DeleteSelectedOrders()
        {
            if (salesOrdersAfterSearch.Count > 0)
            {
                try
                {
                    salesOrdersDb = salesOrdersDb.Where(o => salesOrdersAfterSearch.Where(or => or.IsChecked == true).Any(p => p.Id == o.Id)).ToList();
                    await SalesOrderRepository.DeleteRange(salesOrdersDb);
                    await GetOrders();
                    isSelected = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete selected orders error:" + ex.Message);
                }
            }
        }

        public async Task DeleteCompletedOrders()
        {
            if (salesOrdersAfterSearch.Count > 0)
            {
                try
                {
                    salesOrdersDb = salesOrdersDb.Where(o => salesOrdersAfterSearch.Where(or => or.OrderStatus == OrderStatus.Completed).Any(p => p.Id == o.Id)).ToList();
                    await SalesOrderRepository.DeleteRange(salesOrdersDb);
                    await GetOrders();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete completed orders error:" + ex.Message);
                }
            }
        }

    }
}
