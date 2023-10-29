using AutoMapper;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class Products
    {
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private List<ProductModel> products;
        private List<ProductModel> productsAfterSearch = new();

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
    }

}
