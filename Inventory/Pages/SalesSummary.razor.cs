using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class SalesSummary
    {
        [Parameter] public string SalesId { get; set; }
        [Parameter] public string SalesSummaryId { get; set; }

        [Inject] public ISaleRepository SaleRepository { get; set; }

        private SalesEntity salesEntity = new();
        private SalesSummaryEntity salesSummaryEntity = new();
        private SalesSummaryVariantModel salesSummaryModel = new();
        public List<SalesSummaryVariantModel> SalesSummaryModelVariants { get; set; }

        protected async override Task OnInitializedAsync()
        {
            if (SalesId != null)
            {
                salesEntity = await SaleRepository.GetById(Guid.Parse(SalesId));
                if (salesEntity != null)
                {
                    salesSummaryEntity.VoucherId = salesEntity.VoucherId;
                    salesSummaryEntity.Customer = salesEntity.Customer;
                    salesSummaryEntity.Date = salesEntity.Date;
                    var list = salesEntity.SalesVariants.GroupBy(p => p.Product).Select(p => new SalesSummaryVariantEntity()
                    {
                        Product = p.Key,
                       Quantity= p.Sum(p=>p.Quantity.Value)
                    }).ToList();

                }
            }
        }


    }
}
