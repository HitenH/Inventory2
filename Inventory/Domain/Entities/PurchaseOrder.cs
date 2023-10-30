using Inventory.Models;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseOrder
    {
        [Key]
        public Guid Id { get; set; }
        public SupplierEntity? Supplier { get; set; }
        public ProductEntity? Product { get; set; }
        public int? Quantity { get; set; }
        public DateOnly? Date { get; set; }
        public DateOnly? DueDate { get; set; }
        public OrderStatus? OrderStatus { get; set; }
        public string? Remarks { get; set; }
    }
}
