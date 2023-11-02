using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class PurchaseModel
    {
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public SupplierEntity Supplier { get; set; }
        public DateOnly Date { get; set; }
        public string? Remarks { get; set; }
        public List<PurchaseVariant> PurchaseVariants { get; set; } = new();
    }
}
