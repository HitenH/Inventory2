using AutoMapper;
using BlazorBootstrap;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Inventory.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Inventory.Pages
{
    public partial class Product
    {
        [Parameter] public string ProductId { get; set; }

        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ICategoryRepository CategoryRepository { get; set; }
        [Inject] private ILogger<Product> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private ProductModel productModel = new();
        private ProductEntity productEntity = new();
        private List<CategoryModel> categories = new();
        private Guid categoryId = Guid.Empty;
        public VariantModel variant = new();
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;

        private ModalWindow modalWindowComponenRef;
        private string titleMessage = string.Empty;
        private string errorMessageShort = string.Empty;
        private string errorMessageFull = string.Empty;


        protected async override Task OnInitializedAsync()
        {
            editContext = new(productModel);
            messageStore = new(editContext);
            editContext.OnValidationStateChanged += HandleValidationRequested;

            try
            {
                if (ProductId != null)
                {
                    productEntity = await ProductRepository.GetById(Guid.Parse(ProductId));
                    if (productEntity == null)
                        navManager.NavigateTo("/products");
                    if (productEntity.Category != null)
                        categoryId = productEntity.Category.Id;
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

        private void HandleValidationRequested(object? sender, ValidationStateChangedEventArgs args)
        {
            messageStore?.Clear();

            if (String.IsNullOrEmpty(productModel.Name))
                messageStore?.Add(() => productModel.Name!, "The Product name is required!");

            if (productModel.Rate == null)
                messageStore?.Add(() => productModel.Rate!, "The Rate is required!");

            if (String.IsNullOrEmpty(productModel.ProductId))
                messageStore?.Add(() => productModel.ProductId!, "The ProductID is required!");
            else
            {
                var isExistProductId = false;
                if (productModel.Id == Guid.Empty)
                    isExistProductId = ProductRepository.IsProductIdExist(productModel.ProductId);
                else
                    isExistProductId = ProductRepository.IsProductIdExist(productModel.ProductId, productModel.Id);

                if (isExistProductId)
                    messageStore?.Add(() => productModel.ProductId!, "The ProductID exists in the database!");
            }
        }

        public async Task AddProduct()
        {
            if (editContext != null && editContext.Validate())
            {
                if (productModel != null)
                {
                    try
                    {
                        productEntity = Mapper.Map<ProductEntity>(productModel);
                        productEntity.Category = await GetCategory(categoryId);
                        await ProductRepository.Create(productEntity);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Create Product error: " + ex.Message);
                    }
                }
                navManager.NavigateTo("/products");
            }
        }

        public async Task UpdateProduct()
        {
            if (editContext != null && editContext.Validate())
            {
                if (productModel != null)
                {
                    try
                    {
                        productEntity.ProductId = productModel.ProductId;
                        productEntity.Name = productModel.Name;
                        productEntity.Category = await GetCategory(categoryId);
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
        }

        public async void DeleteProduct()
        {
            if (productEntity != null)
            {
                try
                {
                    await ProductRepository.Delete(productEntity);
                    navManager.NavigateTo("/products");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete Product error: " + ex.Message);
                    titleMessage = "Error Message";
                    errorMessageShort = "Cannot delete Product";
                    errorMessageFull = ex.Message;

                    if (modalWindowComponenRef != null)
                        await modalWindowComponenRef.OnShowModalClick();
                }
            }
           
        }

        public async Task<CategoryEntity> GetCategory(Guid id)
        {
            return await CategoryRepository.GetById(categoryId);
        }

        public void GetVariant(VariantModel model)
        {
            variant = model;
        }

        public void Dispose()
        {
            if (editContext != null)
                editContext.OnValidationStateChanged -= HandleValidationRequested;
        }

        public void CloseModalWindow(bool isClosed)
        {
            if (isClosed)
                ClearErrorMessage();
        }

        private void ClearErrorMessage()
        {
            titleMessage = string.Empty;
            errorMessageShort = string.Empty;
            errorMessageFull = string.Empty;
        }
    }
}
