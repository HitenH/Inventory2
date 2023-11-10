using AutoMapper;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Pages;
using Microsoft.AspNetCore.Components;

namespace Inventory.Shared
{
    public partial class ProductPopup
    {
        [Parameter] public bool IsVisible { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ILogger<ProductPopup> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        [Parameter] public EventCallback<bool> CloseCallBack { get; set; }
        [Parameter] public EventCallback<ProductModel> ProductCallBack { get; set; }

        private List<ProductModel> products = new();
        private List<ProductModel> productsAfterSearch = new();
        private bool isSortAscending = false;

        protected async override Task OnParametersSetAsync()
        {
            await Task.Delay(200);
            try
            {
                var productsDb = await ProductRepository.GetAll();
                if (productsDb.Count != 0)
                {
                    products = productsDb.Select(p => Mapper.Map<ProductModel>(p)).ToList();
                    productsAfterSearch = products;
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError("Product popup error: " + ex.Message);
            }
        }

        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            productsAfterSearch = products.Where(n => n.ProductId.ToLower().Contains(search) || n.Name.ToLower().Contains(search)).ToList();
        }

        private void Close()
        {
            IsVisible = false;
            CloseCallBack.InvokeAsync(IsVisible);
            StateHasChanged();
        }

        public async Task SelectProduct(ProductModel product)
        {
            await ProductCallBack.InvokeAsync(product);
            Close();
        }

        public void SortItem(string column)
        {
            if (productsAfterSearch.Count != 0)
            {
                if (column == "ProductId")
                {
                    if (isSortAscending)
                    {
                        productsAfterSearch = productsAfterSearch.OrderBy(c => c.ProductId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        productsAfterSearch = productsAfterSearch.OrderByDescending(c => c.ProductId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Name")
                {
                    if (isSortAscending)
                    {
                        productsAfterSearch = productsAfterSearch.OrderBy(c => c.Name).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        productsAfterSearch = productsAfterSearch.OrderByDescending(c => c.Name).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }
}
