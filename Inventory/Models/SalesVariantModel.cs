using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class SalesVariantModel
    {
        public Guid Id { get; set; }
        [Required]
        public string ProductId { get; set; }
        [Required]
        public string VariantId { get; set; }
        public int? Quantity { get; set; } = 1;
        public string ProductName { get; set; }
        public ProductEntity Product { get; set; }
        public VariantEntity Variant { get; set; }
    }
}
