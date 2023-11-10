using Inventory.Domain.Entities;

namespace Inventory.Models
{
    public class SalesSummaryModel
    {
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        public Guid ProductEntityId { get; set; }
        public ProductEntity Product { get; set; }
        public int Quantity { get; set; }
        public decimal ProductRate { get; set; }
        public decimal Amount { get; set; }
        public int Discount { get; set; }
        public decimal AmountAfterDiscount { get; set; }
    }
}
