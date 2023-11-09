using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class ProductEntity
    {
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
        public List<SalesSummaryVariantEntity>? SalesSummaryVariants { get; set; } = new();
    }
}
