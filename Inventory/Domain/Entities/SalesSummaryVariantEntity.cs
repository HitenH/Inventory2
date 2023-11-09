namespace Inventory.Domain.Entities
{
    public class SalesSummaryVariantEntity
    {
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        public Guid ProductEntityId { get; set; }
        public ProductEntity Product { get; set; } = new();
        public int Quantity { get; set; } = default(int);
        public decimal ProductRate { get; set; } = default(decimal);
        public decimal Amount { get; set; } = default(decimal);
        public decimal Discount { get; set; } = default(int);
        public decimal AmountAfterDiscount { get; set; } = default(decimal);
        public Guid SalesSummaryEntityId { get; set; }
        public SalesSummaryEntity SalesSummary { get; set; }
    }
}
