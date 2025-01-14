using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class VariantModel
    {
        public Guid Id { get; set; }
        public string? VariantId { get; set; }
        public string? Name { get; set; }
        public int? StockInHand { get; set; }
        public int? ReorderLevel { get; set; }
        public Image? Image { get; set; } = new();
        public List<PurchaseVariant>? PurchaseVariants { get; set; }
        public List<SalesVariantEntity>? SalesVariants { get; set; }
    }
}
