using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class VariantEntity
    { 
        [Key]
        public Guid Id { get; set; }
        public string? VariantId { get; set; }
        public string? Name { get; set; }
        public int? StockInHand { get; set; } = default(int);
        public Image? Image { get; set; }
        public Guid? ProductEntityId { get; set; }
        public ProductEntity? Product { get; set; }
        public List<PurchaseVariant>? PurchaseVariants { get; set; }
    }
}
