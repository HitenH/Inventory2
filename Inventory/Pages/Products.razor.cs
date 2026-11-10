using AutoMapper;
using ClosedXML.Excel;
using CsvHelper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Globalization;

namespace Inventory.Pages;

public partial class Products
{
    [Inject] private IProductRepository ProductRepository { get; set; }
    [Inject] private ILogger<Products> Logger { get; set; }
    [Inject] private IMapper Mapper { get; set; }
    [Inject] ISnackbar Snackbar {get; set;}
    [Inject] IDialogService DialogService {get; set;}
    [Inject] ICategoryRepository CategoryRepository { get; set; }

    private List<ProductModel> products = new();
    private List<ProductModel> productsAfterSearch = new();
    private bool isLoaded;
    private IDialogReference dialogReference;

    protected async override Task OnInitializedAsync()
    {
        try
        {
            var list = await ProductRepository.GetAllForLookup();
            products = list
                .Where(p => !string.IsNullOrWhiteSpace(p.ProductId))
                .Select(c => Mapper.Map<ProductModel>(c))
                .OrderBy(o => o.Name)
                .ToList();
            productsAfterSearch = products;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Products page error");
        }
        finally
        {
            isLoaded = true;
        }
    }

    public void SearchItem(ChangeEventArgs e)
    {
        var search = e.Value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            productsAfterSearch = products;
            return;
        }

        productsAfterSearch = products.Where(n =>
            (n.ProductId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (n.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (n.Rate.HasValue && n.Rate.Value.ToString("G").Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    //file import
    private async Task HandleFilesChanged(IBrowserFile file)
    {
        if (file == null)
        {
            Console.WriteLine("No file selected.");
            return;
        }

        try
        {
            await ShowLoadingDialogAsync();

            using var stream = file.OpenReadStream();

            if (file.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                file.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                await ImportFromExcel(stream);
            }
            else if (file.ContentType == "text/csv" ||
                     file.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                await ImportFromCsv(stream);
            }
            else
            {
                Console.WriteLine("Unsupported file type.");
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            CloseDialog();
        }
    }

    private async Task ImportFromExcel(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheet(1);
        var rowCount = worksheet.LastRowUsed().RowNumber();

        for (int row = 2; row <= rowCount; row++)
        {
            var product = new ProductModel
            {
                Id = Guid.NewGuid(),
                ProductId = worksheet.Cell(row, 1).GetValue<string>(),
                Name = worksheet.Cell(row, 2).GetValue<string>(),
                Rate = worksheet.Cell(row, 3).GetValue<decimal>(),
                Description = worksheet.Cell(row, 4).GetValue<string>(),
                CategoryEntityId = worksheet.Cell(row, 5).GetValue<Guid?>()
            };

            await SaveProductAsync(product);
            Snackbar.Add($"Imported product {product.Name}", Severity.Info);
        }

        Snackbar.Add("Products imported successfully", Severity.Success);
    }

    private async Task ImportFromCsv(Stream stream)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture));

        var records = csv.GetRecords<ProductModel>();

        foreach (var product in records)
        {
            await SaveProductAsync(product);
        }

        Snackbar.Add("Products imported successfully", Severity.Success);
    }

    private async Task ShowLoadingDialogAsync()
    {
        var options = new DialogOptions() { CloseButton = false, MaxWidth = MaxWidth.ExtraSmall };

        dialogReference = await DialogService.ShowAsync<LoadingDialog>("Adding products...", options);
    }

    private void CloseDialog()
    {
        dialogReference.Close();
    }

    private async Task SaveProductAsync(ProductModel productModel)
    {
        if (productModel != null)
        {
            try
            {
                ProductEntity productEntity = Mapper.Map<ProductEntity>(productModel);

                productEntity.Category = await CategoryRepository.GetById(productModel.CategoryEntityId ?? Guid.Empty);

                await ProductRepository.Create(productEntity);
            }
            catch (Exception ex)
            {
                Snackbar.Add("Error while saving product", Severity.Error);
            }
        }
    }
}