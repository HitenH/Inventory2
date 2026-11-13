using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Pages;

public partial class Products
{
    [Inject] private IProductRepository ProductRepository { get; set; }
    [Inject] private ILogger<Products> Logger { get; set; }

    private List<ProductModel> products = new();
    private List<ProductModel> productsAfterSearch = new();
    private bool isLoaded;

    protected async override Task OnInitializedAsync()
    {
        try
        {
            var list = await ProductRepository.GetAllForLookup();
            products = list
                .Where(p => !string.IsNullOrWhiteSpace(p.ProductId))
                .Select(c => c.ToModel())
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

    // Batch 14: the list shows one page at a time (see ListPaging).
    private MudBlazor.MudTable<ProductModel>? listTable;

    public void SearchItem(ChangeEventArgs e)
    {
        listTable?.NavigateTo(0);   // a new search starts on page 1 (batch 14)
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
}
