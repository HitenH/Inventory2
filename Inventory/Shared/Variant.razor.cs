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
    // Stage L: the label icon next to the reorder level shows only for people who may print labels.
    [CascadingParameter] private Inventory.Service.UserAccess? Access { get; set; }
    [Inject] private IVariantRepository VariantRepository { get; set; }
    [Inject] private IProductRepository ProductRepository { get; set; }
    [Inject] private IImageRepository ImageRepository { get; set; }
    [Inject] private IConfiguration config { get; set; }
    [Inject] private PlaceService PlaceService { get; set; } = default!;

    private VariantModel variantModel = new();
    private Image image = new();
    private List<VariantModel> variants = new();
    // Stage 2: sellable, damaged and waiting pieces per variant (shown by StockFigure).
    private Dictionary<Guid, Inventory.Domain.StockFigures> stockInHand = new();
    // Stage 3, batch 3-3: the sellable pieces per place (only the places this person sees), behind the stock arrow.
    private Dictionary<Guid, List<Inventory.Domain.PlaceStock>> placeStock = new();
    // Stage 3: the racks each variant is kept on.
    private Dictionary<Guid, List<VariantRackItem>> racksByVariant = new();
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
        await LoadRacksAsync();
    }

    private async Task LoadRacksAsync() =>
        racksByVariant = Product?.Variants is { Count: > 0 }
            ? await PlaceService.RacksOfVariantsAsync(Product.Variants.Select(v => v.Id))
            : new Dictionary<Guid, List<VariantRackItem>>();

    // Stage 3, batch 3-2 (Hiten, 26 Nov 2026): the variant line that is open (its strip with Print labels / Where it is kept).
    private Guid? expandedId;

    private bool CanOpen(VariantModel variant) => Product?.Id != Guid.Empty && variant.Id != Guid.Empty;

    private void RowClicked(TableRowClickEventArgs<VariantModel> e)
    {
        if (e.Item is { } variant && CanOpen(variant))
        {
            expandedId = expandedId == variant.Id ? null : variant.Id;
        }
    }

    private string RowClass(VariantModel variant, int index) =>
        !CanOpen(variant) ? "" : expandedId == variant.Id ? "variant-line variant-open" : "variant-line";

    /// <summary>Stage 3, batch 3-2: the "Where this item is kept" pop-up for one variant.</summary>
    private async Task OpenRacksAsync(VariantModel variant)
    {
        var others = (Product?.Variants?.Count ?? 1) - 1;
        var canChange = Access?.Allows(Permissions.StockRacks) == true;
        if (await VariantRacksDialog.ShowAsync(DialogService, $"Where {Product?.ProductId} / {variant.VariantId} is kept", variant.Id, others, canChange))
        {
            await LoadRacksAsync();
        }
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
            Snackbar.Add("Could not delete the variant: " + ex.Message, Severity.Error);
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
            Snackbar.Add($"Could not upload the file: {ex.Message}");
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
                // The variant open in the edit form holds its own copy of the photo; forget that one too.
                if (variantModel?.Image?.Id == image.Id)
                    variantModel.Image = null;
                GetVariants();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Could not delete the file: {ex.Message}");
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
            stockInHand = new Dictionary<Guid, Inventory.Domain.StockFigures>();
            placeStock = new Dictionary<Guid, List<Inventory.Domain.PlaceStock>>();
            return;
        }

        // Stage 3, batch 3-3 (Hiten): a person limited to some places sees the stock of those places only.
        var ids = Product.Variants.Select(v => v.Id).ToList();
        stockInHand = await ProductRepository.GetStockByVariantIdsAsync(ids, Access?.Places);
        placeStock = await ProductRepository.GetPlaceStockAsync(ids, Access?.Places);
    }

    public void Dispose()
    {
        if (editContext != null)
            editContext.OnValidationStateChanged -= HandleValidationRequested;
    }
}
