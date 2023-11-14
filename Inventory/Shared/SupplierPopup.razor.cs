using AutoMapper;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Shared
{
    public partial class SupplierPopup
    {
        [Parameter] public bool IsVisible { get; set; }
        [Inject] private ISupplierRepository SupplierRepository { get; set; }
        [Inject] private ILogger<SupplierPopup> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        [Parameter] public EventCallback<bool> CloseCallBack { get; set; }
        [Parameter] public EventCallback<SupplierModel> SupplierCallBack { get; set; }

        private List<SupplierModel> suppliers = new();
        private List<SupplierModel> suppliersAfterSearch = new();
        private bool isSortAscending = false;


        protected async override Task OnParametersSetAsync()
        {
            try
            {
                var suppliersDb = await SupplierRepository.GetAll();
                if (suppliersDb.Count != 0)
                {
                    suppliers = suppliersDb.Select(s => Mapper.Map<SupplierModel>(s)).ToList();
                    suppliersAfterSearch = suppliers;
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError("Supplier popup error: " + ex.Message);
            }
        }
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            suppliersAfterSearch = suppliers.Where(n => n.SupplierId.ToLower().Contains(search) || n.Name.ToLower().Contains(search)).ToList();
        }

        private void Close()
        {
            IsVisible = false;
            CloseCallBack.InvokeAsync(IsVisible);
            StateHasChanged();
        }

        public async Task SelectSupplier(SupplierModel supplier)
        {
            await SupplierCallBack.InvokeAsync(supplier);
            Close();
        }
        public void SortItem(string column)
        {
            if (suppliersAfterSearch.Count != 0)
            {
                if (column == "SuptomerId")
                {
                    if (isSortAscending)
                    {
                        suppliersAfterSearch = suppliersAfterSearch.OrderBy(c => c.SupplierId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        suppliersAfterSearch = suppliersAfterSearch.OrderByDescending(c => c.SupplierId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Name")
                {
                    if (isSortAscending)
                    {
                        suppliersAfterSearch = suppliersAfterSearch.OrderBy(c => c.Name).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        suppliersAfterSearch = suppliersAfterSearch.OrderByDescending(c => c.Name).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }
}
