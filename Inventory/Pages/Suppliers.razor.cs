using AutoMapper;
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
    [Inject] private IMapper Mapper { get; set; }
    [Inject] private NavigationManager navManager { get; set; }

    private List<SuppliersDisplayModel> suppliersDisplayModels = new();
    private List<SuppliersDisplayModel> suppliersAfterSearch = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var list = await SupplierRepository.GetAll();
                if (list.Count != 0)
                {
                    foreach (var item in list)
                    {
                        suppliersDisplayModels.Add(new SuppliersDisplayModel
                        {
                            Id = item.Id,
                            SupplierId = item.SupplierId,
                            Name = item.Name,
                            Mobiles = item.Mobiles,
                            Area = item.Area,
                            TotalAmount = item.Purchases.Sum(purchase => purchase.TotalAmountProduct ?? 0)
                        });
                    }

                    suppliersAfterSearch = suppliersDisplayModels;
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
        suppliersAfterSearch = suppliersDisplayModels.Where(n => n.SupplierId.ToLower().Contains(search)
                    || n.Name.ToLower().Contains(search)
                    || n.Area.ToLower().Contains(search)
                    || n.Mobiles.Any(p => p.Phone.ToLower().Contains(search))).ToList();
    }


    private void RowClickEvent(TableRowClickEventArgs<SuppliersDisplayModel> tableRowClickEventArgs)
    {
        navManager.NavigateTo($"supplier/{tableRowClickEventArgs.Item.Id}");
    }


    class SuppliersDisplayModel()
    {
        public Guid Id { get; set; }
        public string SupplierId { get; set; }
        public string Name { get; set; }
        public List<Mobile> Mobiles { get; set; }
        public string Area { get; set; }
        public decimal TotalAmount { get; set; }
    }
}

