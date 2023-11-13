using AutoMapper;
using BlazorBootstrap;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Inventory.Shared
{
    public partial class Variant
    {
        [Parameter] public ProductEntity Product { get; set; }
        [Inject] private IVariantRepository VariantRepository { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private IImageRepository ImageRepository { get; set; }
        [Inject] private ILogger<Variant> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IConfiguration config { get; set; }

        private VariantModel variantModel = new();
        private Image image = new();
        private List<VariantModel> variants = new();
        private bool isSortAscending = false;
        private Dictionary<Guid, int> stockInHand = new();
        private Modal? modal = new();
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;
        private bool bclearinputfile = false;

        protected override void OnInitialized()
        {
            editContext = new(variantModel);
            messageStore = new(editContext);
            editContext.OnValidationStateChanged += HandleValidationRequested;
        }
        protected override void OnParametersSet()
        {
            GetVariants();
            GetStockInHandAmount();
        }

        private void HandleValidationRequested(object? sender, ValidationStateChangedEventArgs args)
        {
            messageStore?.Clear();

            if (String.IsNullOrEmpty(variantModel.Name))
                messageStore?.Add(() => variantModel.Name!, "The Variant name is required!");

            if (String.IsNullOrEmpty(variantModel.VariantId))
                messageStore?.Add(() => variantModel.VariantId!, "The VariantID is required!");
            else
            {
                if(Product.Id != Guid.Empty)
                {
                    var isExistVariantId = false;
                    if (variantModel.Id == Guid.Empty)
                        isExistVariantId = VariantRepository.IVariantIdExist(variantModel.VariantId, Product.Id);
                    else
                        isExistVariantId = VariantRepository.IVariantIdExist(variantModel.VariantId, Product.Id, variantModel.Id);

                    if (isExistVariantId)
                        messageStore?.Add(() => variantModel.VariantId!, "For this Product the VariantID exists in the database!");
                }
                
            }
        }

        public async Task AddVariant()
        {
            if (editContext != null && editContext.Validate())
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
                        GetStockInHandAmount();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Create variant: " + ex.Message);
                    }
                }
            }   
        }

        public async Task EditVariant()
        {
            if (editContext != null && editContext.Validate())
            {
                if (variantModel != null)
                {
                    try
                    {
                        if (image.Id != Guid.Empty)
                        {
                            var img = await ImageRepository.GetById(variantModel.Image.Id);
                            if (img != null)
                            {
                                img.ImageTitle = image.ImageTitle;
                                img.ImageData = image.ImageData;
                                variantModel.Image = img;
                            }
                        }
                        else if (image.ImageData != null)
                        {
                            variantModel.Image = image;
                        }

                        var variant = Product.Variants.FirstOrDefault(v => v.Id == variantModel.Id);
                        if (variant != null)
                        {
                            variant.VariantId = variantModel.VariantId;
                            variant.Name = variantModel.Name;
                            //variant.StockInHand = variantModel.StockInHand;
                            variant.Image = variantModel.Image;
                            await ProductRepository.Update(Product);
                            CancelVariant();
                            GetVariants();
                            GetStockInHandAmount();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Create variant: " + ex.Message);
                    }
                }
            }
        }

        public void CancelVariant()
        {
            messageStore?.Clear();
            variantModel = new();
            image = new();
            ClearInputFile();
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
                    await VariantRepository.Delete(variant);
                    GetVariants();
                    GetStockInHandAmount();
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

        private void ClearInputFile()
        {
            bclearinputfile = true;
            StateHasChanged();
            bclearinputfile = false;
            StateHasChanged();
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

        private async Task OpenImageModal(byte[]? imageBytes)
        {

        }

        public void GetStockInHandAmount()
        {
            if (variants.Count != 0)
            {
                var arrival = variants.Select(variant => new
                {
                    VariantId = variant.Id,
                    Quantity = variant.PurchaseVariants.Sum(purchase => purchase.Quantity ?? 0)
                }).ToDictionary(result => result.VariantId, result => result.Quantity);

                var shipment = variants.Select(variant => new
                {
                    VariantId = variant.Id,
                    Quantity = variant.SalesVariants.Sum(sale => sale.Quantity ?? 0)
                }).ToDictionary(result => result.VariantId, result => result.Quantity);

                stockInHand = arrival.Join(shipment,
                    arrivalItem => arrivalItem.Key,
                    shipmentItem => shipmentItem.Key,
                    (arrivalItem, shipmentItem) => new
                    {
                        VariantId = arrivalItem.Key,
                        Quantity = arrivalItem.Value - shipmentItem.Value
                    }).ToDictionary(result => result.VariantId, result => result.Quantity);
            }
        }

        private async Task OnShowModalClick()
        {
            await modal?.ShowAsync();
        }

        private async Task OnHideModalClick()
        {
            await modal?.HideAsync();
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

        public void Dispose()
        {
            if (editContext != null)
                editContext.OnValidationStateChanged -= HandleValidationRequested;
        }
    }
}
