using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    [Authorize]
    public partial class Category
    {
        [Inject] private ICategoryRepository CategoryRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private CategoryModel category;

        protected override void OnInitialized()
        {
            category = new();
        }

        public async Task CreateCategory()
        {
            try
            {
                if (category != null)
                {
                    await CategoryRepository.Create(Mapper.Map<CategoryEntity>(category));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Create category: " + ex.Message);
            }
            finally
            {
                navManager.NavigateTo("/products");
            }
        }
    }
}
