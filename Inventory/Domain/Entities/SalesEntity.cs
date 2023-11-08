using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class SalesEntity
    {
        [Key]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid CustomerEntityId { get; set; }
        public CustomerEntity Customer { get; set; } = new();
        public DateOnly Date { get; set; }
        public List<SalesVariantEntity>? SalesVariants { get; set; } = new();
    }
}
