using AutoMapper;
using BlazorBootstrap;
using DocumentFormat.OpenXml.Spreadsheet;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace Inventory.Shared;

public partial class Variant
{
    [Parameter] public ProductEntity Product { get; set; }
    [Inject] private IVariantRepository VariantRepository { get; set; }
    [Inject] private IProductRepository ProductRepository { get; set; }
    [Inject] private IImageRepository ImageRepository { get; set; }
    [Inject] private IMapper Mapper { get; set; }
    [Inject] private IConfiguration config { get; set; }

    private VariantModel variantModel = new();
    private Image image = new();
    private List<VariantModel> variants = new();
    private Dictionary<Guid, int> stockInHand = new();
    private EditContext? editContext;
    private MudTextField<string?> variantInputRef = null!;
    private ValidationMessageStore? messageStore;
    private bool bclearinputfile = false;
    private Snackbar snackbar;


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
            if (Product.Id != Guid.Empty)
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
                    await ReturnFocusToInput();
                }
                catch (Exception ex)
                {
                    snackbar = Snackbar.Add($"Create variant error: {ex.Message}");
                }
            }
        }
    }

    //Open Image Popup
    private async Task OpenImagePopup(Image image)
    {
        var parameters = new DialogParameters();
        parameters.Add("ImageHere", image);
        var dialog = DialogService.Show<ImagePopup>(image.ImageTitle, parameters);
        var result = await dialog.Result;
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
                        variant.ReorderLevel = variantModel.ReorderLevel;
                        variant.Image = variantModel.Image;
                        await ProductRepository.Update(Product);
                        CancelVariant();
                        GetVariants();
                        GetStockInHandAmount();
                    }
                }
                catch (Exception ex)
                {
                    snackbar = Snackbar.Add($"Error: {ex.Message}");
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
            snackbar = Snackbar.Add("Delete variant error: " + ex.Message, Severity.Error);
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
            snackbar = Snackbar.Add($"Upload file error: {ex.Message}");
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
                snackbar = Snackbar.Add($"Delete file error: {ex.Message}");
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

    private async Task ReturnFocusToInput()
    {
        if (variantInputRef != null)
        {
            await variantInputRef.FocusAsync();
        }
    }

    public void GetStockInHandAmount()
    {
        stockInHand = variants.ToDictionary(variant => variant.Id, variant =>
            (variant.PurchaseVariants.Sum(p => p.Quantity ?? 0)) -
            (variant.SalesVariants.Sum(s => s.Quantity ?? 0))
        );
    }

    public void Dispose()
    {
        if (editContext != null)
            editContext.OnValidationStateChanged -= HandleValidationRequested;
    }
}
