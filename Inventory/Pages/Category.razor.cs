using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages
{
    [Authorize]
    public partial class Category
    {
        [Inject] private ICategoryRepository CategoryRepository { get; set; }
        [Inject] private ILogger<Category> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private ISnackbar Snackbar { get; set; } = default!;

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
                    await CategoryRepository.Create(category.ToEntity());
                }
                navManager.NavigateTo("/products");
            }
            catch (Exception ex)
            {
                // Stay on the page so the operator sees the save failed and can try again.
                Logger.LogError(ex, "Create category");
                Snackbar.Add("Could not save the category. " + ex.Message, Severity.Error);
            }
        }
    }
}
