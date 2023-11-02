using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid? SupplierId { get; set; }
        public SupplierEntity? Supplier { get; set; }
        public DateOnly Date { get; set; }
        public string? Remarks { get; set; }
        public IEnumerable<PurchaseVariant> PurchaseVariants { get; set; }
    }
}
