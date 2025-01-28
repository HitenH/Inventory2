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

namespace Inventory.Pages
{
    public partial class Products
    {
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private ILogger<Products> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] IMobileService MobileService {get; set;}
        [Inject] ISnackbar Snackbar {get; set;}
        [Inject] IDialogService DialogService {get; set;}
        [Inject] ICategoryRepository CategoryRepository { get; set; }

        private List<ProductModel> products;
        private List<ProductModel> productsAfterSearch = new();
        private bool isSortAscending = false;
        private IDialogReference dialogReference;

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                products = new();
                try
                {
                    var list = await ProductRepository.GetAll();
                    if (list.Count != 0)
                    {
                        products = list.Select(c => Mapper.Map<ProductModel>(c)).ToList();
                        productsAfterSearch = [.. products.OrderByDescending(o => o.Name)];
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Products error" + ex.Message);
                }
            }
        }
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            productsAfterSearch = products.Where(n => n.ProductId.ToLower().Contains(search)
                            || n.Name.ToLower().Contains(search)
                            || (n.Rate.HasValue && n.Rate.Value.ToString("G").ToLower().Contains(search))).ToList();
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
                // Implement Excel parsing logic
                await ShowLoadingDialogAsync();


                using var stream = file.OpenReadStream();

                // Check file type
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
                //Snackbar message
                Snackbar.Add($"Error: {ex.Message}", Severity.Error);
            }
            finally
            {
                CloseDialog();
            }
        }

        private async Task ImportFromExcel(Stream stream)
        {

            // Copy the asynchronous stream to a memory stream
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // Reset the position to the beginning of the stream

            // Use the memory stream with ClosedXML
            using var workbook = new XLWorkbook(memoryStream);
            var worksheet = workbook.Worksheet(1); // Select the first worksheet
            var rowCount = worksheet.LastRowUsed().RowNumber();

            for (int row = 2; row <= rowCount; row++) // Assuming row 1 contains headers
            {
                var product = new ProductModel
                {
                    Id = Guid.NewGuid(),
                    ProductId = worksheet.Cell(row, 1).GetValue<string>(), // Column 1
                    Name = worksheet.Cell(row, 2).GetValue<string>(),      // Column 2
                    Rate = worksheet.Cell(row, 3).GetValue<decimal>(),    // Column 3
                    Description = worksheet.Cell(row, 4).GetValue<string>(), // Column 4
                    CategoryEntityId = worksheet.Cell(row, 5).GetValue<Guid?>() // Column 5
                };

                // Add the customer to the database or a collection
                await SaveProductAsync(product);

                // Snackbar of short duration of progress
                Snackbar.Add($"Imported product {product.Name}", Severity.Info);
            }
            //Snackbar of completion
            Snackbar.Add("Products imported successfully", Severity.Success);
        }

        private async Task ImportFromCsv(Stream stream)
        {
            // Implement CSV parsing logic
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture));

            var records = csv.GetRecords<ProductModel>();

            foreach (var product in records)
            {
                await SaveProductAsync(product);
            }
            //Snackbar of completion
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
                    //Snackbar message
                    Snackbar.Add("Error while saving product", Severity.Error);
                }
            }
        }
    }
}
