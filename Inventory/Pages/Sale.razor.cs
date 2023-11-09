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

        private List<ProductEntity> products = new();


        protected async override Task OnInitializedAsync()
        {
            products = await ProductRepository.GetAll();
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
                        Id= v.Id,
                        Product = v.Product,
                        Variant = v.ProductVariant,
                        ProductId = v.Product.ProductId,
                        ProductName = v.Product.Name,
                        Quantity = v.Quantity
                    }).ToList();
                    IsDisabled = false;
                }
            }
            else
                IsDisabled = true;
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
                    salesEntity = await SaleRepository.GetById(id);
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

        public void AddVariant()
        {
            if (salesVariantModel.ProductId != null && salesVariantModel.VariantId != null)
            {
                var productDb = products.FirstOrDefault(p => p.ProductId == salesVariantModel.ProductId);
                if (productDb != null && productDb.Variants.Count != 0)
                {
                    var productVariant = productDb.Variants.FirstOrDefault(v => v.VariantId == salesVariantModel.VariantId);
                    if (productVariant != null)
                    {
                        salesVariantModel.VariantId = productVariant.VariantId;
                        salesVariantModel.ProductId = productDb.ProductId;
                        salesVariantModel.ProductName = productDb.Name;
                        salesVariantModel.Product = productDb;
                        salesVariantModel.Variant = productVariant;
                        salesModelVariants.Add(salesVariantModel);
                    }
                }
            }
            CancelSalesVariant();
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
    }
}
