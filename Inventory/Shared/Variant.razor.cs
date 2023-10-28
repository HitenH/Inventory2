using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Inventory.Shared
{
    public partial class Variant
    {
        [Parameter] public ProductEntity Product { get; set; }
        [Inject] private IVariantRepository VariantRepository { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }

        private VariantModel variantModel = new();
        private Image image = new();
        private List<VariantModel> variants = new();

        protected async override Task OnInitializedAsync()
        {
            if (Product != null && Product.Variants.Count != 0)
            {
                variants = Product.Variants.Select(v => Mapper.Map<VariantModel>(v)).ToList();
            }
        }
        public async Task AddVariant()
        {
            if (variantModel != null)
            {
                try
                {
                    variantModel.Image = image;
                    Product.Variants.Add(Mapper.Map<VariantEntity>(variantModel));
                    await ProductRepository.Update(Product);
                    //await VariantRepository.Create(Mapper.Map<VariantEntity>(variantModel));
                    CancelVariant();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Create variant: " + ex.Message);
                }
            }
        }

        public void CancelVariant()
        {
            variantModel = new();
            image = new();
        }
        public async Task UpdateVariant()
        {

        }

        public async Task DeleteVariant()
        {

        }

        public async Task UploadFile(InputFileChangeEventArgs eventArgs)
        {
            image.ImageTitle = eventArgs.File.Name;
            using Stream stream = eventArgs.File.OpenReadStream();
            using MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            image.ImageData = ms.ToArray();

        }
    }
}
