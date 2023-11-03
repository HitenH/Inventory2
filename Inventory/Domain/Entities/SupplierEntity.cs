using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class SupplierEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string? SupplierId { get; set; }
        public string? Name { get; set; }
        public string? ContactPerson { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? Remarks { get; set; }
        public List<Mobile>? Mobiles { get; set; }
        public List<PurchaseEntity>? Purchases { get; set; }
    }
}
