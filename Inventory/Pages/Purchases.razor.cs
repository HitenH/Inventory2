using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    [Authorize]
    public partial class Purchases
    {
        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
        [Inject] private ILogger<Purchases> Logger { get; set; }

        private List<PurchaseModel> purchases = new();
        private List<PurchaseModel> purchasesAfterSearch = new();

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    var list = await PurchaseRepository.GetAll();
                    if (list.Count != 0)
                    {
                        purchases = list.Select(c =>
                         new PurchaseModel()
                         {
                             Date = c.Date,
                             Id = c.Id,
                             Remarks = c.Remarks,
                             SupplierId = c.Supplier.SupplierId,
                             SupplierName = c.Supplier.Name,
                             VoucherId = c.VoucherId,
                             TotalAmountProduct = c.TotalAmountProduct
                         }
                        ).ToList();
                        purchasesAfterSearch = [.. purchases.OrderByDescending(o => o.Date)];
                    }
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Purchases error");
                }
            }
        }
       
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            purchasesAfterSearch = purchases.Where(n => n.VoucherId.ToString().Contains(search) || n.SupplierName.ToLower().Contains(search)
                                                        || n.Date.ToString().Contains(search)).ToList();
        }
    }
}
