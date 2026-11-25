using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class SupplierEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
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
