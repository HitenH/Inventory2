using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class SalesVariantEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid ProductEntityId { get; set; }
        public ProductEntity Product { get; set; } = new();
        public Guid VariantEntityId { get; set; }
        public VariantEntity ProductVariant { get; set; }
        public int? Quantity { get; set; } = default(int);
        public Guid SalesEntityId { get; set; }
        public SalesEntity Sale { get; set; }
    }
}
