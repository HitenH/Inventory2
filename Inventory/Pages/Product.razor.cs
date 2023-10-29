using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Inventory.Pages
{
    public partial class Product
    {
        [Parameter] public string ProductId { get; set; }

        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ICategoryRepository CategoryRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IMobileService NumberService { get; set; }

        private ProductModel productModel = new();
        private ProductEntity productEntity = new();
        private List<CategoryModel> categories = new();
        private string categoryId = Guid.Empty.ToString();
        public VariantModel variant = new();

        protected async override Task OnInitializedAsync()
        {
            try
            {
                if (ProductId != null)
                {
                    productEntity = await ProductRepository.GetById(Guid.Parse(ProductId));
                    if (productEntity == null)
                        navManager.NavigateTo("/products");

                    productModel = Mapper.Map<ProductModel>(productEntity);
                }
                var categoriesDb = await CategoryRepository.GetAll();
                if (categoriesDb.Count != 0)
                    categories = categoriesDb.Select(c => Mapper.Map<CategoryModel>(c)).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError("Product error:" + ex.Message);
            }

        }

        public async Task AddProduct()
        {
            if (productModel != null)
            {
                try
                {
                    productEntity = Mapper.Map<ProductEntity>(productModel);
                    productEntity.Categoty = await GetCategor(categoryId);
                    await ProductRepository.Create(productEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Create Product error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/products");
        }

        public async Task UpdateProduct()
        {
            if (productModel != null)
            {
                try
                {
                    productEntity.ProductId = productModel.ProductId;
                    productEntity.Name = productModel.Name;
                    productEntity.Categoty = await GetCategor(categoryId);
                    productEntity.Description = productModel.Description;
                    productEntity.Rate = productModel.Rate;

                    await ProductRepository.Update(productEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update Product error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/products");
        }

        public async void DeleteProduct()
        {
            if (productEntity != null)
            {
                try
                {
                    await ProductRepository.Delete(productEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete Product error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/products");
        }

        public async Task<CategoryEntity> GetCategor(string id)
        {
            return await CategoryRepository.GetById(Guid.Parse(categoryId));
        }

        public void GetVariant(VariantModel model)
        {
            variant = model;
        }
    }
}
