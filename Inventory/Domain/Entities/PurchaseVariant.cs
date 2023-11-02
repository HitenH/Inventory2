using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseVariant
    {
        [Key]
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        public Guid? ProductId { get; set; }
        public ProductEntity? Product { get; set; } = new();
        public string? VariantId { get; set; }
        public int? Quantity { get; set; } = default(int);
        public decimal? ProductRate { get; set; } = default(decimal);
        public decimal? Amount { get; set; } = default(decimal);
        public int? Discount { get; set; } = default(int);
        public decimal? AmountAfterDiscount { get; set; } = default(decimal);
    }
}
