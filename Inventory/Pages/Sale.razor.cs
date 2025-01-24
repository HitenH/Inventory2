using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.MudBlazorComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace Inventory.Pages;

public partial class Sale
{
    [Parameter] public string SaleId { get; set; }
    [Inject] private ISaleRepository SaleRepository { get; set; }
    [Inject] private ICustomerRepository CustomerRepository { get; set; }
    [Inject] private IProductRepository ProductRepository { get; set; }
    [Inject] private ILogger<Sale> Logger { get; set; }
    [Inject] private NavigationManager navManager { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private IDialogService DialogService { get; set; }
    [Inject] private IMapper Mapper { get; set; }

    private bool IsDisabled { get; set; }
    private CustomerEntity customer = new();
    private SalesModel salesModel = new();
    private SalesEntity salesEntity = new();


    private ProductModel selectedProduct;
    private CustomerModel customerModel;

    private SalesVariantModel salesVariantModel = new();
    private List<SalesVariantModel> salesModelVariants = new();
    private List<SalesVariantEntity> salesEntityVariants = new();

    private ProductEntity productEntity = new();
    private ElementReference productInput;

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

            if (SaleId != null)
            {
                salesEntity = await SaleRepository.GetById(Guid.Parse(SaleId));
                if (salesEntity != null)
                {
                    salesModel.VoucherId = salesEntity.VoucherId;
                    salesModel.CustomerId = salesEntity.Customer.CustomerId;
                    salesModel.CustomerName = salesEntity.Customer.Name;
                    salesModel.Date = salesEntity.Date;
                    salesModel.Id= salesEntity.Id;

                    customer = salesEntity.Customer;
                    salesModelVariants = salesEntity.SalesVariants.Select(v => new SalesVariantModel()
                    {
                        Id = v.Id,
                        ProductEntityId = v.ProductEntityId,
                        ProductId = v.Product.ProductId,
                        ProductName = v.Product.Name,
                        Product = v.Product,
                        VariantEntityId = v.VariantEntityId,
                        VariantId = v.ProductVariant.VariantId,
                        Variant = v.ProductVariant,
                        Quantity = v.Quantity,
                        IsCreated = true
                    }).ToList();

                    customerModel = Mapper.Map<CustomerModel>(customer);

                    IsDisabled = false;  
                }
            }
            else
                IsDisabled = true;
            await FocusInput();
            StateHasChanged();
        }
    }
    private async Task FocusInput()
    {
        await JSRuntime.InvokeVoidAsync("setFocus", productInput);
    }

    public async Task AddOrder()
    {
        if (salesModel != null && salesModelVariants.Count != 0)
        {
            try
            {
                var voucherIdDb = SaleRepository.GetLastVoucherIdByDate(salesModel.Date);
                if (voucherIdDb == 0)
                    salesModel.VoucherId = 1;
                else
                    salesModel.VoucherId = voucherIdDb + 1;

                salesEntityVariants = salesModelVariants.Select(v => new SalesVariantEntity()
                {
                    Product = v.Product,
                    ProductVariant = v.Variant,
                    Quantity = v.Quantity
                }).ToList();

                salesEntity = new SalesEntity()
                {
                    Date = salesModel.Date,
                    Customer = customer,
                    VoucherId = salesModel.VoucherId,
                    SalesVariants = salesEntityVariants
                };
                var id = await SaleRepository.Create(salesEntity);
                Snackbar.Add("Operation successful", Severity.Success);
                IsDisabled = false;
                navManager.NavigateTo($"/salessummary/{id}");
            }
            catch (Exception ex)
            {
                Snackbar.Add("Something went wrong", Severity.Warning);
                navManager.NavigateTo("/sales");
            }
        }
        else
            navManager.NavigateTo("/sales");
    }

    public async Task EditOrder()
    {
        if (salesModel != null && salesEntity != null)
        {
            try
            {
                var isVoucherExist = false;
                if (salesEntity.VoucherId != salesModel.VoucherId)
                    isVoucherExist = SaleRepository.IsVoucherExistByDate(salesModel.VoucherId, salesModel.Date);

                if (!isVoucherExist)
                    salesEntity.VoucherId = salesModel.VoucherId;
                else
                    salesModel.VoucherId = salesEntity.VoucherId;

                if (salesEntity.Customer.Id != customer.Id)
                    salesEntity.Customer = customer;

                salesEntity.Date = salesModel.Date;
                salesEntity.SalesVariants = salesModelVariants.Select(v => new SalesVariantEntity()
                {
                    Product = v.Product,
                    ProductVariant = v.Variant,
                    Quantity = v.Quantity,
                }).ToList();

                await SaleRepository.Update(salesEntity);
                navManager.NavigateTo($"/salessummary/{salesEntity.Id}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Update sale: " + ex.Message);
                navManager.NavigateTo("/sales");
            }
        }
        else
            navManager.NavigateTo("/sales");
    }

    public void CheckIfClickEnter(KeyboardEventArgs keyBoard)
    {
        if (keyBoard.Key == "Enter")
            AddVariant();
    }

    public async Task AddVariant()
    {
        if (salesVariantModel.ProductId != null && (salesVariantModel.VariantId != null || salesVariantModel.VariantEntityId != null))
        {
            var productDb = await ProductRepository.GetByProductId(salesVariantModel.ProductId);
            if (productDb != null && productDb.Variants.Count != 0)
            {
                var productVariant = new VariantEntity();
                if (salesVariantModel.VariantEntityId != null)
                    productVariant = productDb.Variants.FirstOrDefault(v => v.Id == salesVariantModel.VariantEntityId);
                else
                    productVariant = productDb.Variants.FirstOrDefault(v => v.VariantId == salesVariantModel.VariantId);

                var lastId = 1;
                if (salesModelVariants.Count >0)
                {
                    lastId = salesModelVariants.OrderByDescending(i => i.InnerId).First().InnerId + 1;
                }
               
                if (productVariant != null)
                {
                    salesVariantModel.VariantId = productVariant.VariantId;
                    salesVariantModel.ProductId = productDb.ProductId;
                    salesVariantModel.ProductName = productDb.Name;
                    salesVariantModel.ProductEntityId = productDb.Id;
                    salesVariantModel.VariantEntityId = productVariant.Id;
                    salesVariantModel.Product = productDb;
                    salesVariantModel.Variant = productVariant;
                    salesVariantModel.IsCreated= true;
                    salesVariantModel.InnerId = lastId;
                    salesModelVariants.Add(salesVariantModel);
                }
            }
        }
        CancelSalesVariant();
        await productInput.FocusAsync();
        await FocusInput();
        StateHasChanged();
    }

    public async Task EditVariant()
    {
        if (salesVariantModel != null)
        {
            int indexElement = -1;
            if(salesVariantModel.Id != Guid.Empty)
            {
                indexElement = salesModelVariants.FindIndex(i => i.Id == salesVariantModel.Id);
            } else if (salesVariantModel.InnerId != 0)
            {
                indexElement = salesModelVariants.FindIndex(i => i.InnerId == salesVariantModel.InnerId);
            }

            if (indexElement != -1)
            {
                if (productEntity != null && productEntity.Variants.Count != 0)
                {
                    var productVariant = new VariantEntity();
                    if (salesVariantModel.VariantEntityId != null)
                        productVariant = productEntity.Variants.FirstOrDefault(v => v.Id == salesVariantModel.VariantEntityId);
                    else
                        productVariant = productEntity.Variants.FirstOrDefault(v => v.VariantId == salesVariantModel.VariantId);

                    if (productVariant != null)
                    {
                        salesModelVariants[indexElement].VariantId = productVariant.VariantId;

                        salesModelVariants[indexElement].ProductId = productEntity.ProductId;
                        salesModelVariants[indexElement].ProductName = productEntity.Name;
                        salesModelVariants[indexElement].ProductEntityId = productEntity.Id;
                        salesModelVariants[indexElement].VariantEntityId = productVariant.Id;
                        salesModelVariants[indexElement].Product = productEntity;
                        salesModelVariants[indexElement].Variant = productVariant;
                        salesVariantModel.IsCreated = true;
                    }
                }
            }
        }
        CancelSalesVariant();
        StateHasChanged();
    }
    public void CancelSalesVariant()
    {
        salesVariantModel = new();
    }

    public void DeleteVariant(SalesVariantModel model)
    {
        salesModelVariants.Remove(model);
    }

    public void ClearProductchoice()
    {
        CancelSalesVariant();
        salesModelVariants = new();
    }

    public async void UpdateSalesVariant(SalesVariantModel model)
    {
        CancelSalesVariant();
        salesVariantModel = model;
        productEntity = model.Product;
    }

    private async Task OnCustomerSelected(CustomerModel? _customer)
    {
        if (_customer != null)
        {
            customerModel = _customer;

            customer = await CustomerRepository.GetById(_customer.Id);

            salesModel.CustomerId = _customer.CustomerId;
            salesModel.CustomerName = _customer.Name;
        }
        else
        {
            customerModel = null;
            customer = new();
            // Clear the fields if no product is selected
            salesModel.CustomerId = "";
            salesModel.CustomerName = "";
        }
    }

    public async void OpenScanMassDialogAsync()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            CloseOnEscapeKey = true,
            CloseButton = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<ScanMassProducts>("Scan Dialog", options);
        var result = await dialog.Result;

        //For each scanned item from the returned scanned items list, add it to the salesModelVariants list
        if (!result.Canceled)
        {
            var scannedItems = result.Data as List<ScannedItem>;
            foreach (var item in scannedItems)
            {
                var productDb = await ProductRepository.GetByProductId(item.ProductId);
                if (productDb != null && productDb.Variants.Count != 0)
                {
                    var productVariant = productDb.Variants.FirstOrDefault(v => v.VariantId == item.VariantId);

                    var lastId = 1;
                    if (salesModelVariants.Count > 0)
                    {
                        lastId = salesModelVariants.OrderByDescending(i => i.InnerId).First().InnerId + 1;
                    }
                    if (productVariant != null)
                    {
                        salesModelVariants.Add(new SalesVariantModel()
                        {
                            ProductId = productDb.ProductId,
                            ProductName = productDb.Name,
                            ProductEntityId = productDb.Id,
                            VariantId = productVariant.VariantId,
                            VariantEntityId = productVariant.Id,
                            Product = productDb,
                            Variant = productVariant,
                            Quantity = 1,
                            IsCreated = true,
                            InnerId = lastId
                        });
                    }
                }
            }
            StateHasChanged();
        }
    }


    private async Task OnProductSelected(ProductModel? _product)
    {
        if (_product != null)
        {
            productEntity = await ProductRepository.GetById(_product.Id);
            salesVariantModel.ProductId = _product.ProductId;
            salesVariantModel.ProductName = _product.Name;
        }
    }
}
