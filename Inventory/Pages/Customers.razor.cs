using AutoMapper;
using ClosedXML.Excel;
using CsvHelper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Globalization;
using Inventory.MudBlazorComponents;

namespace Inventory.Pages
{
    public partial class Customers
    {
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Customers> Logger { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private ISnackbar Snackbar { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IDialogService DialogService { get; set; }
        [Inject] private IMobileService MobileService { get; set; }

        private List<CustomerModel> customers = new();
        private List<CustomerModel> customersAfterSearch = new();
        private IDialogReference dialogReference;
        private Dictionary<Guid, decimal> totalAmount = new();

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    var list = await CustomerRepository.GetAll();
                    if (list.Count != 0)
                    {
                        customers = list.Select(c => Mapper.Map<CustomerModel>(c)).ToList();
                        customersAfterSearch = [.. customers.OrderBy(o => o.Name)];
                        GetTotalAmount();
                        StateHasChanged();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Customers page error" + ex.Message);
                }
            }
        }
        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            customersAfterSearch = customers.Where(n => n.CustomerId.ToLower().Contains(search)
                            || n.Name.ToLower().Contains(search)
                            || n.Mobiles.Any(x => x.Phone.ToLower().Contains(search))
                            || n.Area.ToLower().Contains(search)).ToList();
        }

        public void GetTotalAmount()
        {
            if (customersAfterSearch.Count != 0)
            {
                totalAmount = customersAfterSearch.Select(customer => new
                {
                    CustomerId = customer.Id,
                    TotalAmount = customer.Sales.Sum(sale => sale.TotalAmountProduct)
                }).ToDictionary(result => result.CustomerId, result => result.TotalAmount);
            }
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

                //get the value of mobiles from column 7, split by commas and convert to list
                var mobiles = worksheet.Cell(row, 7).GetValue<string>().Split(',').Select(x => new Mobile { Phone = x }).ToList();


                var customer = new CustomerModel
                {

                    Id = Guid.NewGuid(),
                    CustomerId = worksheet.Cell(row, 1).GetValue<string>(), // Column 1
                    Name = worksheet.Cell(row, 2).GetValue<string>(),       // Column 2
                    ContactPerson = worksheet.Cell(row, 3).GetValue<string>(), // Column 3
                    Address = worksheet.Cell(row, 4).GetValue<string>(),    // Column 4
                    Area = worksheet.Cell(row, 5).GetValue<string>(),       // Column 5
                    Remarks = worksheet.Cell(row, 6).GetValue<string>(),   // Column 6
                    Mobiles = mobiles,
                };

                // Add the customer to the database or a collection
                await SaveCustomer(customer);

                //Snackbar of progress
                Snackbar.Add($"Imported {row - 1}", Severity.Info);
                StateHasChanged();
            }

            //Snackbar of completion
            Snackbar.Add("Import completed", Severity.Success);
        }

        private async Task ImportFromCsv(Stream stream)
        {
            // Implement CSV parsing logic
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture));

            var records = csv.GetRecords<CustomerModel>();

            foreach (var customer in records)
            {
                await SaveCustomer(customer);
            }
            //Snackbar of completion
            Snackbar.Add("Import completed", Severity.Success);
        }

        private async Task ShowLoadingDialogAsync()
        {

            var options = new DialogOptions() { CloseButton = false, MaxWidth = MaxWidth.ExtraSmall };

            dialogReference = await DialogService.ShowAsync<LoadingDialog>("Adding customers...", options);
        }

        private void CloseDialog()
        {
            dialogReference.Close();
        }

        private async Task SaveCustomer(CustomerModel customer)
        {
            CustomerEntity customerEntity = Mapper.Map<CustomerEntity>(customer);
            var numbers = MobileService.GetMobiles(customerEntity.Mobiles);
            customerEntity.Mobiles = new();

            if (numbers != null)
                customerEntity.Mobiles.AddRange(numbers);

            await CustomerRepository.Create(customerEntity);
        }


        private void RowClickEvent(TableRowClickEventArgs<CustomerModel> tableRowClickEventArgs)
        {
            navManager.NavigateTo($"customer/{tableRowClickEventArgs.Item.Id}");
        }
    }
}
