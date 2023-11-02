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
        public decimal? Rate { get; set; }
        public string? Description { get; set; }
        public CategoryEntity? Category { get; set; }
        public List<VariantEntity>? Variants { get; set; } = new();
    }
}
