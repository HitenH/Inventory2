using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class ProductModel
    {
        public Guid Id { get; set; }
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal? Rate { get; set; } = default(decimal);
        public string? Description { get; set; }
        /// <summary>The product's variants (filled when they were loaded, e.g. after picking a product).</summary>
        public List<VariantModel> Variants { get; set; } = new();
    }
}
