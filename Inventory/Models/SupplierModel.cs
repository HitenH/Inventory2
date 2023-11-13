using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class SupplierModel
    {
        public Guid Id { get; set; }
        public string SupplierId { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string Address { get; set; }
        public string Area { get; set; }
        public string Remarks { get; set; }
        public List<Mobile> Mobiles { get; set; } = new();
        public List<PurchaseEntity> Purchases { get; set; }
    }
}
