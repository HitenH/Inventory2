using Inventory.Domain.Entities;

namespace Inventory.Models
{
    public class SalesSummaryVariantModel
    {
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public ProductEntity Product { get; set; } = new();
        public int Quantity { get; set; } = default(int);
        public decimal ProductRate { get; set; } = default(decimal);
        public decimal Amount { get; set; } = default(decimal);
        public decimal Discount { get; set; } = default(int);
        public decimal AmountAfterDiscount { get; set; } = default(decimal);
    }
}
