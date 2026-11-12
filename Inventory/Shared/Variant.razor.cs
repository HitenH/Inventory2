using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Inventory.Service;
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
    [Inject] private IConfiguration config { get; set; }

    private VariantModel variantModel = new();
    private Image image = new();
    private List<VariantModel> variants = new();
    private Dictionary<Guid, int> stockInHand = new();
    private EditContext? editContext;
    private MudTextField<string?> variantInputRef = null!;
    private ValidationMessageStore? messageStore;
    private bool bclearinputfile = false;


    protected override void OnInitialized()
    {
        editContext = new(variantModel);
        messageStore = new(editContext);
        editContext.OnValidationStateChanged += HandleValidationRequested;
    }
    protected override async Task OnParametersSetAsync()
    {
        GetVariants();
        await LoadStockInHandAsync();
    }

    private void HandleValidationRequested(object? sender, ValidationStateChangedEventArgs args)
    {
        messageStore?.Clear();

        // Spaces around an ID or name are removed; an ID or name of only spaces counts as empty (batch 5).
        variantModel.VariantId = variantModel.VariantId?.Trim()!;
        variantModel.Name = variantModel.Name?.Trim()!;

        if (String.IsNullOrWhiteSpace(variantModel.Name))
            messageStore?.Add(() => variantModel.Name!, "The Variant name is required!");

        if (String.IsNullOrWhiteSpace(variantModel.VariantId))
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
                VariantEntity? newVariant = null;
                try
                {
                    if (image.ImageData != null)
                        variantModel.Image = image;
                    else
                        variantModel.Image = null;

                    newVariant = variantModel.ToEntity();
                    Product.Variants.Add(newVariant);
                    await ProductRepository.Update(Product);
                    CancelVariant();
                    GetVariants();
                    await LoadStockInHandAsync();
                    await ReturnFocusToInput();
                }
                catch (Exception ex)
                {
                    // The failed variant must not stay in the product's list, or the next save would try it again.
                    if (newVariant != null)
                        Product.Variants?.Remove(newVariant);
                    Snackbar.Add(DbErrors.SaveMessage(ex, "variant", "Variant ID for this product"), Severity.Error);
                }
            }
        }
    }

    //Open Image Popup
    private async Task OpenImagePopup(Image image)
    {
        var parameters = new DialogParameters();
        parameters.Add("ImageHere", image);
        var dialog = await DialogService.ShowAsync<ImagePopup>(image.ImageTitle, parameters);
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
                        await LoadStockInHandAsync();
                    }
                }
                catch (Exception ex)
                {
                    Snackbar.Add(DbErrors.SaveMessage(ex, "variant", "Variant ID for this product"), Severity.Error);
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
                Product.Variants.RemoveAll(v => v.Id == id);
                GetVariants();
                await LoadStockInHandAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add("Delete variant error: " + ex.Message, Severity.Error);
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
            Snackbar.Add($"Upload file error: {ex.Message}");
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
                // Forget the deleted photo on screen too, so the next save of this variant cannot bring it back.
                foreach (var v in Product.Variants ?? new List<VariantEntity>())
                {
                    if (v.Image?.Id == image.Id)
                        v.Image = null;
                }
                GetVariants();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Delete file error: {ex.Message}");
            }
        }
    }

    public void GetVariants()
    {
        if (Product != null)
            variants = Product.Variants.Select(v => v.ToModel()).ToList();
    }

    public string GetImage(Image image) => Photos.UrlFor(image);   // batch 14: a normal image address, not packed into the page

    private async Task ReturnFocusToInput()
    {
        if (variantInputRef != null)
        {
            await variantInputRef.FocusAsync();
        }
    }

    private async Task LoadStockInHandAsync()
    {
        if (Product?.Variants is not { Count: > 0 })
        {
            stockInHand = new Dictionary<Guid, int>();
            return;
        }

        stockInHand = await ProductRepository.GetStockInHandByVariantIdsAsync(
            Product.Variants.Select(v => v.Id));
    }

    public void Dispose()
    {
        if (editContext != null)
            editContext.OnValidationStateChanged -= HandleValidationRequested;
    }
}
