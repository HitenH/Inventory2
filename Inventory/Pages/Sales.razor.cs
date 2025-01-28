using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages
{
    [Authorize]
    public partial class Sales
    {
        [Inject] private ISaleRepository SaleRepository { get; set; }
        [Inject] private ILogger<Sales> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }

        private List<SalesModel> sales = new();
        private List<SalesModel> salesAfterSearch = new();

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
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
                             Id = c.Id
                         }
                        ).ToList();
                        salesAfterSearch = [.. sales.OrderByDescending(o => o.Date)];
                    }
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Sales error" + ex.Message);
                }
            }
        }

        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            salesAfterSearch = sales.Where(n => n.VoucherId.ToString().Contains(search) || n.CustomerName.ToLower().Contains(search)
                                                        || n.Date.ToString().Contains(search)).ToList();
        }

        private void RowClickEvent(TableRowClickEventArgs<SalesModel> tableRowClickEventArgs)
        {
            navManager.NavigateTo($"salessummary/{tableRowClickEventArgs.Item.Id}");
        }
    }
}
