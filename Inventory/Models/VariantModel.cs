using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class VariantModel
    {
        public Guid Id { get; set; }
        [Required]
        public string? VariantId { get; set; }
        [Required]
        public string? Name { get; set; }
        public int? StockInHand { get; set; }
        public Image? Image { get; set; } = new();
        public List<PurchaseVariant>? PurchaseVariants { get; set; }
        public List<SalesVariantEntity>? SalesVariants { get; set; }
    }
}
