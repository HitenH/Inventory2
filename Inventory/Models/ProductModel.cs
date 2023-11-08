using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class ProductModel
    {
        public Guid Id { get; set; }
        [Required]
        public string? ProductId { get; set; }
        [Required]
        public string? Name { get; set; }
        [Required]
        public decimal? Rate { get; set; }
        public string? Description { get; set; }
        public Guid? CategoryEntityId { get; set; }
        public CategoryEntity? Category { get; set; } = new();
        public List<VariantEntity>? Variants { get; set; } = new();
        public List<PurchaseVariant>? PurchaseVariants { get; set; } = new();
    }
}
