using AutoMapper;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class Products
    {
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ILogger<Products> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private List<ProductModel> products;
        private List<ProductModel> productsAfterSearch = new();
        private bool isSortAscending = false;

        protected async override Task OnInitializedAsync()
        {
            products = new();
            try
            {
                var list = await ProductRepository.GetAll();
                if (list.Count != 0)
                {
                    products = list.Select(c => Mapper.Map<ProductModel>(c)).ToList();
                    productsAfterSearch = products;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Products error" + ex.Message);
            }
        }
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            productsAfterSearch = products.Where(n => n.ProductId.ToLower().Contains(search) || n.Name.ToLower().Contains(search)).ToList();
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
                else if (column == "Rate")
                {
                    if (isSortAscending)
                    {
                        productsAfterSearch = productsAfterSearch.OrderBy(c => c.Rate).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        productsAfterSearch = productsAfterSearch.OrderByDescending(c => c.Rate).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }

}
