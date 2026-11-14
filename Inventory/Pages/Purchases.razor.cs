using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages
{
    [Authorize]
    public partial class Purchases
    {
        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
        [Inject] private ILogger<Purchases> Logger { get; set; }
        [Inject] private IDialogService DialogService { get; set; } = default!;

        private List<PurchaseModel> purchases = new();
        private List<PurchaseModel> purchasesAfterSearch = new();

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await LoadAsync();
            }
        }

        private async Task LoadAsync()
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

        // Import pop-up (batch 8): on a successful import the list is loaded again so the new purchases show.
        private async Task OpenImportAsync()
        {
            if (await Inventory.MudBlazorComponents.ImportDialog.ShowAsync(DialogService, ImportKind.Purchases))
            {
                await LoadAsync();
                StateHasChanged();
            }
        }
       
        // Batch 14: the list shows one page at a time (see ListPaging).
        private MudBlazor.MudTable<PurchaseModel>? listTable;

        public void SearchItem(ChangeEventArgs e)
        {
            listTable?.NavigateTo(0);   // a new search starts on page 1 (batch 14)
            var search = e.Value.ToString().ToLower();
            purchasesAfterSearch = purchases.Where(n => n.VoucherId.ToString().Contains(search) || n.SupplierName.ToLower().Contains(search)
                                                        || n.Date.ToString().Contains(search)).ToList();
        }
    }
}
