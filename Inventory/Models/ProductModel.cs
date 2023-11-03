using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class ProductModel
    {
        public Guid Id { get; set; }
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal? Rate { get; set; }
        public string? Description { get; set; }
        public Guid? CategoryEntityId { get; set; }
        public CategoryEntity? Category { get; set; } = new();
        public List<VariantEntity>? Variants { get; set; } = new();
        public List<PurchaseVariant>? PurchaseVariants { get; set; } = new();
    }
}
