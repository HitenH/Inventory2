using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class ProductEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        [Key]
        public Guid Id { get; set; }
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal? Rate { get; set; }
        public string? Description { get; set; }
        public CategoryEntity? Category { get; set; }
        public List<VariantEntity>? Variants { get; set; } = new();
        public List<PurchaseVariant>? PurchaseVariants { get; set; } = new();
        public List<SalesOrderVariantEntity>? SalesOrderVariants { get; set; } = new();
        public List<SalesVariantEntity>? SalesVariants { get; set; } = new();
    }
}
