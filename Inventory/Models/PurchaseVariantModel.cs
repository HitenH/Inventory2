using Inventory.Domain.Entities;

namespace Inventory.Models
{
    public class PurchaseVariantModel
    {
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        public Guid ProductEntityId { get; set; }
        public string? VariantId { get; set; }
        public int? Quantity { get; set; } = default(int);
        public decimal? ProductRate { get; set; } = default(decimal);
        public decimal? Amount { get; set; } = default(decimal);
        public int? Discount { get; set; } = default(int);
        public decimal? AmountAfterDiscount { get; set; } = default(decimal);
        //public int? PurchaseVoucherId { get; set; }
        //public PurchaseEntity? Purchase { get; set; }
    }
}
