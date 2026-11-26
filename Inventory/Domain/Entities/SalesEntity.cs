using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class SalesEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        [Key]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid CustomerEntityId { get; set; }
        public CustomerEntity? Customer { get; set; }
        public DateOnly Date { get; set; }
        public List<SalesVariantEntity>? SalesVariants { get; set; } = new();
        public List<SalesSummaryEntity>? SalesSummaries { get; set; } = new();
        public string? Remarks { get; set; }
        /// <summary>Stage 3: the place the goods left from (entries before stage 3: the shop, <see cref="Places.StartingId"/>).</summary>
        public int PlaceId { get; set; } = Places.StartingId;
        public decimal TotalQuantity { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("Discount")]
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmountProduct { get; set; }
    }
}
