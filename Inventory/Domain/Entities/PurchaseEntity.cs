using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid SupplierEntityId { get; set; }
        public SupplierEntity? Supplier { get; set; }
        public DateOnly Date { get; set; }
        public string? Remarks { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("Discount")]
        public decimal? DiscountAmount { get; set; }
        public decimal? TotalAmountProduct { get; set; }
        public List<PurchaseVariant>? PurchaseVariants { get; set; } = new();
    }
}
