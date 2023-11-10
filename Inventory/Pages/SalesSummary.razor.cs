using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class SalesSummary
    {
        [Parameter] public string SalesId { get; set; }
        [Inject] public ISaleRepository SaleRepository { get; set; }

        private SalesEntity salesEntity = new();
        private SalesSummaryEntity salesSummaryEntity = new();
        public List<SalesSummaryEntity> salesSummaryEntityList = new();
        public List<SalesSummaryModel> salesSummaryModelList = new();
        private ComponentTotalData SalesTotalData = new();


        protected async override Task OnInitializedAsync()
        {
            if (SalesId != null)
            {
                salesEntity = await SaleRepository.GetById(Guid.Parse(SalesId));
                if (salesEntity != null)
                {
                    salesSummaryModelList = salesEntity.SalesVariants.GroupBy(p => p.Product).Select(p => new SalesSummaryModel()
                    {
                        Product = p.Key,
                        Quantity = p.Sum(p => p.Quantity.Value),
                    }).ToList();
                    if (salesSummaryModelList.Count != 0)
                    {
                        salesSummaryEntityList = salesSummaryModelList.Select(p => new SalesSummaryEntity()
                        {
                            ProductEntityId = p.Product.Id,
                            ProductName = p.Product.Name,
                            ProductId = p.Product.ProductId,
                            Quantity = p.Quantity,
                            ProductRate = p.Product.Rate.Value
                        }).ToList();

                        for (int i = 0; i < salesSummaryEntityList.Count; i++)
                        {
                            salesSummaryEntityList[i].SerialNumber = i + 1;
                            if (salesSummaryEntityList[i].ProductRate == 0 || salesSummaryEntityList[0].Quantity == 0)
                                salesSummaryEntityList[i].Amount = 0;
                            else
                                salesSummaryEntityList[i].Amount = Math.Round(salesSummaryEntityList[i].ProductRate * salesSummaryEntityList[i].Quantity, 2);
                            salesSummaryEntityList[i].AmountAfterDiscount = salesSummaryEntityList[i].Amount;
                        }
                        GetTotalAmount();
                    }
                }
            }
        }

        public async Task AddSales()
        {

        }

        public void RateChanged(ChangeEventArgs arg, Guid productId)
        {
            var index = salesSummaryEntityList.FindIndex(n => n.ProductEntityId == productId);
            var value = decimal.Parse(arg.Value.ToString());
            salesSummaryEntityList[index].ProductRate = value;
            AmountChanged(index);
        }

        public void AmountChanged(int index)
        {
            if (salesSummaryEntityList[index].ProductRate == 0 || salesSummaryEntityList[index].Quantity == 0)
                salesSummaryEntityList[index].Amount = 0;
            else
                salesSummaryEntityList[index].Amount = Math.Round(salesSummaryEntityList[index].ProductRate * salesSummaryEntityList[index].Quantity, 2);
            AmountAfterDiscountChanged(index);
        }

        public void DiscountChanged(ChangeEventArgs arg, Guid productId)
        {
            var index = salesSummaryEntityList.FindIndex(n => n.ProductEntityId == productId);
            var value = int.Parse(arg.Value.ToString());
            salesSummaryEntityList[index].Discount = value;
            AmountAfterDiscountChanged(index);
        }

        public void AmountAfterDiscountChanged(int index)
        {
            if (salesSummaryEntityList[index].Amount != 0 && salesSummaryEntityList[index].Discount != 0)
            {
                var amount = salesSummaryEntityList[index].Amount - (salesSummaryEntityList[index].Amount * ((decimal)salesSummaryEntityList[index].Discount / 100));
                salesSummaryEntityList[index].AmountAfterDiscount = Math.Round(amount, 2);
            }
            else
            {
                salesSummaryEntityList[index].AmountAfterDiscount = Math.Round(salesSummaryEntityList[index].Amount, 2);
            }
        }

        private void DiscountForTotalAmountChanged(decimal value)
        {
            SalesTotalData.Discount = value;
            GetTotalAmount();
        }

        public void GetTotalAmount()
        {
            SalesTotalData.TotalQuantity = salesSummaryEntityList.Select(x => x.Quantity).Sum();
            var amount = salesSummaryEntityList.Select(v => v.AmountAfterDiscount).Sum();
            SalesTotalData.TotalAmount = amount - SalesTotalData.Discount;
        }
    }
}
