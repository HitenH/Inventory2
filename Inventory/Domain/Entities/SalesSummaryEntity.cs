namespace Inventory.Domain.Entities
{
    public class SalesSummaryEntity
    {
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid CustomerEntityId { get; set; }
        public CustomerEntity Customer { get; set; } = new();
        public DateOnly Date { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmountAfterDiscount { get; set; }
        public string? Remarks { get; set; }
        public List<SalesSummaryVariantEntity>? SalesSummaryVariants { get; set; } = new();
        public Guid SalesId { get; set; }
    }
}
