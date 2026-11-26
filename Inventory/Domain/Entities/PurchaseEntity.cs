using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        [Key]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid SupplierEntityId { get; set; }
        public SupplierEntity? Supplier { get; set; }
        public DateOnly Date { get; set; }
        public string? Remarks { get; set; }
        /// <summary>Stage 3: the place the goods came into (entries before stage 3: the shop, <see cref="Places.StartingId"/>).</summary>
        public int PlaceId { get; set; } = Places.StartingId;
    [System.ComponentModel.DataAnnotations.Schema.Column("Discount")]
        public decimal? DiscountAmount { get; set; }
        public decimal? TotalAmountProduct { get; set; }
        public List<PurchaseVariant>? PurchaseVariants { get; set; } = new();
    }
}
