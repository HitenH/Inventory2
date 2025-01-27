using AutoMapper;
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
    [Inject] private IMapper Mapper { get; set; }
    [Inject] private NavigationManager navManager { get; set; }

    private List<SupplierModel> suppliers = new();
    private List<SupplierModel> suppliersAfterSearch = new();
    private Dictionary<Guid,decimal> totalAmount = new();

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var list = await SupplierRepository.GetAll();
                if (list.Count != 0)
                {
                    suppliers = list.Select(c => Mapper.Map<SupplierModel>(c)).ToList();
                    suppliersAfterSearch = [.. suppliers.OrderByDescending(o => o.Name)];
                    GetTotalAmount();
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Suppliers page error" + ex.Message);
            } 
        }
    }
    public void SearchItem(ChangeEventArgs e)
    {
        var search = e.Value.ToString().ToLower();
        suppliersAfterSearch = suppliers.Where(n => n.SupplierId.ToLower().Contains(search)
                    || n.Name.ToLower().Contains(search)
                    || n.Area.ToLower().Contains(search)
                    || n.Mobiles.Any(p => p.Phone.ToLower().Contains(search))).ToList();
    }

    public void GetTotalAmount()
    {
        if (suppliersAfterSearch.Count != 0)
        {
            totalAmount = suppliersAfterSearch.Select(supplier => new
            {
                SupplierId = supplier.Id,
                TotalAmount = supplier.Purchases.Sum(purchase => purchase.TotalAmountProduct ?? 0)
            }).ToDictionary(result => result.SupplierId, result => result.TotalAmount);
        }
    }

    private void RowClickEvent(TableRowClickEventArgs<SupplierModel> tableRowClickEventArgs)
    {
        navManager.NavigateTo($"supplier/{tableRowClickEventArgs.Item.Id}");
    }

}

