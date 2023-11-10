using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Inventory.Pages
{
    public partial class Sale
    {
        [Parameter] public string SaleId { get; set; }
        [Inject] private ISaleRepository SaleRepository { get; set; }
        [Inject] private ICustomerRepository CustomerRepository { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private ILogger<Sale> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }

        private bool isVisibleCustomerPopup = false;
        private bool IsDisabled { get; set; }
        private CustomerEntity customer = new();
        private SalesModel salesModel = new();
        private SalesEntity salesEntity = new();

        private SalesVariantModel salesVariantModel = new();
        private List<SalesVariantModel> salesModelVariants = new();
        private List<SalesVariantEntity> salesEntityVariants = new();

        private ProductEntity productEntity = new();
        private List<ProductEntity> products = new();
        private bool isVisibleProductPopup = false;

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                //products = await ProductRepository.GetAll();
                if (SaleId != null)
                {
                    salesEntity = await SaleRepository.GetById(Guid.Parse(SaleId));
                    if (salesEntity != null)
                    {
                        salesModel.VoucherId = salesEntity.VoucherId;
                        salesModel.SalesVariants = salesEntity.SalesVariants;
                        salesModel.CustomerId = salesEntity.Customer.CustomerId;
                        salesModel.CustomerName = salesEntity.Customer.Name;
                        salesModel.Date = salesEntity.Date;

                        customer = salesEntity.Customer;
                        salesModelVariants = salesEntity.SalesVariants.Select(v => new SalesVariantModel()
                        {
                            Id = v.Id,
                            ProductEntityId = v.Product.Id,
                            VariantEntityId = v.VariantEntityId,
                            ProductId = v.Product.ProductId,
                            ProductName = v.Product.Name,
                            VariantId = v.ProductVariant.VariantId,
                            Quantity = v.Quantity,
                            Product = v.Product,
                            Variant = v.ProductVariant,
                            IsCreated = true
                        }).ToList();
                        IsDisabled = false;
                    }
                }
                else
                    IsDisabled = true;
                StateHasChanged();
            }
        }
  
        public async Task AddOrder()
        {
            if (salesModel != null && salesModelVariants.Count != 0)
            {
                try
                {
                    var voucherIdDb = await SaleRepository.GetLastVoucherIdByDate(salesModel.Date);
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

                    salesEntity = new()
                    {
                        Date = salesModel.Date,
                        Customer = customer,
                        VoucherId = salesModel.VoucherId,
                        SalesVariants = salesEntityVariants
                    };
                    var id = await SaleRepository.Create(salesEntity);
                    SaleId = id.ToString();
                    salesModel.Id = id;
                    //salesEntity = await SaleRepository.GetById(id);
                    IsDisabled = false;
                    navManager.NavigateTo($"/salessummary/{id}");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Add new sale error: " + ex.Message);
                          navManager.NavigateTo("/sales");
                }
            }
        }

        public async Task EditOrder()
        {
            salesEntity = await SaleRepository.GetById(salesModel.Id);
            if (salesModel != null)
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
                        Id = v.Id
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
        }
        public void OpenCustomerPopup()
        {
            isVisibleCustomerPopup = true;
        }

        public void CloseCustomerPopup(bool state)
        {
            isVisibleCustomerPopup = state;
        }

        public async void GetCustomerFromPopup(CustomerModel customerFromPopup)
        {
            if (customerFromPopup != null)
            {
                salesModel.CustomerId = customerFromPopup.CustomerId;
                salesModel.CustomerName = customerFromPopup.Name;
                customer = await CustomerRepository.GetById(customerFromPopup.Id);
            }
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
                    var productDb = await ProductRepository.GetByProductId(salesVariantModel.ProductId);
                    if (productDb != null && productDb.Variants.Count != 0)
                    {
                        var productVariant = new VariantEntity();
                        if (salesVariantModel.VariantEntityId != null)
                            productVariant = productDb.Variants.FirstOrDefault(v => v.Id == salesVariantModel.VariantEntityId);
                        else
                            productVariant = productDb.Variants.FirstOrDefault(v => v.VariantId == salesVariantModel.VariantId);

                        if (productVariant != null)
                        {
                            salesModelVariants[indexElement].VariantId = productVariant.VariantId;

                            salesModelVariants[indexElement].ProductId = productDb.ProductId;
                            salesModelVariants[indexElement].ProductName = productDb.Name;
                            salesModelVariants[indexElement].ProductEntityId = productDb.Id;
                            salesModelVariants[indexElement].VariantEntityId = productVariant.Id;
                            salesModelVariants[indexElement].Product = productDb;
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

        public void OpenProductPopup()
        {
            isVisibleProductPopup = true;
        }

        public void CloseProductPopup(bool state)
        {
            isVisibleProductPopup = state;
        }

        public async Task GetProductFromPopup(ProductModel productFromPopup)
        {
            if (productFromPopup != null)
            {
                salesVariantModel.ProductId = productFromPopup.ProductId;
                salesVariantModel.ProductName = productFromPopup.Name;
                productEntity = await ProductRepository.GetById(productFromPopup.Id);
            }
        }

        public async void UpdateSalesVariant(SalesVariantModel model)
        {
            CancelSalesVariant();
            salesVariantModel = model;
            productEntity = model.Product;
        }

    }
}
