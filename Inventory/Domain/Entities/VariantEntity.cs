using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class VariantEntity
    { 
        [Key]
        public Guid Id { get; set; }
        public string? VariantId { get; set; }
        public string? Name { get; set; }
        public string? StockInHand { get; set; }
        public Guid? ImageId { get; set; }
        public Image? Image { get; set; }
    }
}
