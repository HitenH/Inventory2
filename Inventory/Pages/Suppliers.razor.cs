using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages;

[Authorize]
public partial class Suppliers
{
    [Inject] private ISupplierRepository SupplierRepository { get; set; }
    [Inject] private ILogger<Suppliers> Logger { get; set; }
    [Inject] private NavigationManager navManager { get; set; }

    private List<SuppliersDisplayModel> suppliersDisplayModels = new();
    private List<SuppliersDisplayModel> suppliersAfterSearch = new();
    private bool isLoaded;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var list = await SupplierRepository.GetAll();
            var purchaseTotals = await SupplierRepository.GetPurchaseTotalsBySupplierAsync();

            suppliersDisplayModels = list.Select(item => new SuppliersDisplayModel
            {
                Id = item.Id,
                SupplierId = item.SupplierId ?? "",
                Name = item.Name ?? "",
                Mobiles = item.Mobiles ?? new(),
                Area = item.Area ?? "",
                TotalAmount = purchaseTotals.GetValueOrDefault(item.Id, 0)
            }).ToList();

            suppliersAfterSearch = suppliersDisplayModels;
            isLoaded = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Suppliers page error");
            isLoaded = true;
        }
    }

    // Batch 14: the list shows one page at a time (see ListPaging).
    private MudBlazor.MudTable<SuppliersDisplayModel>? listTable;

    public void SearchItem(ChangeEventArgs e)
    {
        listTable?.NavigateTo(0);   // a new search starts on page 1 (batch 14)
        var search = e.Value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            suppliersAfterSearch = suppliersDisplayModels;
            return;
        }

        suppliersAfterSearch = suppliersDisplayModels.Where(n =>
            (n.SupplierId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (n.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (n.Area?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (n.Mobiles?.Any(p => p.Phone?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ?? false))
            .ToList();
    }

    private void RowClickEvent(TableRowClickEventArgs<SuppliersDisplayModel> tableRowClickEventArgs)
    {
        navManager.NavigateTo($"supplier/{tableRowClickEventArgs.Item.Id}");
    }

    class SuppliersDisplayModel
    {
        public Guid Id { get; set; }
        public string SupplierId { get; set; } = "";
        public string Name { get; set; } = "";
        public List<Mobile> Mobiles { get; set; } = new();
        public string Area { get; set; } = "";
        public decimal TotalAmount { get; set; }
    }
}
