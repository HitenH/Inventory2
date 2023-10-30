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
        [Inject] private IImageRepository ImageRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IConfiguration config { get; set; }

        private VariantModel variantModel = new();
        private Image image = new();
        private List<VariantModel> variants = new();
        private bool isSortAscending = false;

        protected override void OnParametersSet()
        {
            GetVariants();
        }
        public async Task AddVariant()
        {
            if (variantModel != null)
            {
                try
                {
                    if (image.ImageData != null)
                        variantModel.Image = image;
                    else
                        variantModel.Image = null;

                    Product.Variants.Add(Mapper.Map<VariantEntity>(variantModel));
                    await ProductRepository.Update(Product);
                    CancelVariant();
                    GetVariants();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Create variant: " + ex.Message);
                }
            }
        }

        public async Task EditVariant()
        {
            if (variantModel != null)
            {
                try
                {
                    if (image.Id != Guid.Empty)
                    {
                        var img = await ImageRepository.GetById(variantModel.Image.Id);
                        if(img != null){
                            img.ImageTitle = image.ImageTitle;
                            img.ImageData = image.ImageData;
                            variantModel.Image = img;
                        }
                    } else if (image.ImageData != null)
                    {
                        variantModel.Image = image;
                    }
                        
                    var variant = Product.Variants.FirstOrDefault(v => v.Id == variantModel.Id);
                    if (variant != null)
                    {
                        variant.VariantId = variantModel.VariantId;
                        variant.Name = variantModel.Name;
                        variant.StockInHand = variantModel.StockInHand;
                        variant.Image = variantModel.Image;
                        await ProductRepository.Update(Product);
                        CancelVariant();
                        GetVariants();
                    }
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

        public async Task UpdateVariant(VariantModel model)
        {
            CancelVariant();
            variantModel = model;
        }

        public async Task DeleteVariant(Guid id)
        {
            try
            {
                var variant = await VariantRepository.GetById(id);
                if (variant != null)
                {
                    await DeleteFile(variant.Image);
                    await VariantRepository.Delete(variant);
                    GetVariants();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Delete variant error: " + ex.Message);
            }

        }

        public async Task UploadFile(InputFileChangeEventArgs eventArgs)
        {
            try
            {
                if (eventArgs.File.ContentType == "image/jpeg")
                {
                    var resizedFile = await eventArgs.File.RequestImageFileAsync(eventArgs.File.ContentType, 640, 480);
                    image.ImageTitle = resizedFile.Name;
                    var maxSizeUploadedFile = int.Parse(config.GetSection("FileMaxSize").Value);
                    using Stream stream = resizedFile.OpenReadStream(maxSizeUploadedFile);
                    using MemoryStream ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    image.ImageData = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Upload file error: " + ex.Message);
            }
        }

        public async Task DeleteFile(Image image)
        {
            if (image != null)
            {
                try
                {
                    await ImageRepository.Delete(image);
                    GetVariants();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete file error: " + ex.Message);
                }
            }
        }

        public void GetVariants()
        {
            if (Product != null)
                variants = Product.Variants.Select(v => Mapper.Map<VariantModel>(v)).ToList();
        }

        public string GetImage(Image image)
        {
            return $"data:image/jpg;base64,{Convert.ToBase64String(image.ImageData)}";
        }

        public void SortItem(string column)
        {
            if (variants.Count != 0)
            {
                if (column == "VariantId")
                {
                    if (isSortAscending)
                    {
                        variants = variants.OrderBy(c => c.VariantId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        variants = variants.OrderByDescending(c => c.VariantId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Name")
                {
                    if (isSortAscending)
                    {
                        variants = variants.OrderBy(c => c.Name).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        variants = variants.OrderByDescending(c => c.Name).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "StockInHand")
                {
                    if (isSortAscending)
                    {
                        variants = variants.OrderBy(c => c.StockInHand).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        variants = variants.OrderByDescending(c => c.StockInHand).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }
}
