using Inventory.Models;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseOrderEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string? SupplierId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? ProductRate { get; set; }
        public string? VariantId { get; set; }
        public int? Quantity { get; set; }
        public DateOnly? Date { get; set; }
        public DateOnly? DueDate { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public string? Remarks { get; set; }
    }
}
