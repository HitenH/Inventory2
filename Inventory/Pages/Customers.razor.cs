using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Inventory.Service.Import;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages
{
    public partial class Customers
    {
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private ILogger<Customers> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IDialogService DialogService { get; set; } = default!;

        private List<CustomerModel> customers = new();
        private List<CustomerModel> customersAfterSearch = new();
        private Dictionary<Guid, decimal> totalAmount = new();

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await LoadAsync();
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                var list = await CustomerRepository.GetAll();
                var salesTotals = await CustomerRepository.GetSalesTotalsByCustomerAsync();
                if (list.Count != 0)
                {
                    customers = list.Select(c => c.ToModel(salesTotals)).ToList();
                    customersAfterSearch = [.. customers.OrderBy(o => o.Name)];
                    GetTotalAmount();
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Customers page error");
            }
        }

        // Import pop-up (batch 8): on a successful import the list is loaded again so the new customers show.
        private async Task OpenImportAsync()
        {
            if (await Inventory.MudBlazorComponents.ImportDialog.ShowAsync(DialogService, ImportKind.Customers))
            {
                await LoadAsync();
                StateHasChanged();
            }
        }
        // Batch 14: the list shows one page at a time (see ListPaging).
        private MudBlazor.MudTable<CustomerModel>? listTable;

        public void SearchItem(ChangeEventArgs e)
        {
            listTable?.NavigateTo(0);   // a new search starts on page 1 (batch 14)
            var search = e.Value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(search))
            {
                customersAfterSearch = [.. customers.OrderBy(o => o.Name)];
                GetTotalAmount();
                return;
            }

            customersAfterSearch = customers.Where(n => MatchesCustomerSearch(n, search)).ToList();
            GetTotalAmount();
        }

        private static bool MatchesCustomerSearch(CustomerModel n, string term)
        {
            if (n is null || string.IsNullOrEmpty(term))
            {
                return false;
            }

            return Matches(n.CustomerId, term)
                   || Matches(n.Name, term)
                   || Matches(n.Area, term)
                   || (n.Mobiles?.Any(m => Matches(m?.Phone, term)) ?? false);
        }

        private static bool Matches(string? field, string term) =>
            !string.IsNullOrEmpty(field) && field.Contains(term, StringComparison.OrdinalIgnoreCase);

        public void GetTotalAmount()
        {
            if (customersAfterSearch.Count != 0)
            {
                totalAmount = customersAfterSearch.Select(customer => new
                {
                    CustomerId = customer.Id,
                    TotalAmount = customer.TotalSales
                }).ToDictionary(result => result.CustomerId, result => result.TotalAmount);
            }
        }


        private void RowClickEvent(TableRowClickEventArgs<CustomerModel> tableRowClickEventArgs)
        {
            navManager.NavigateTo($"customer/{tableRowClickEventArgs.Item.Id}");
        }
    }
}
