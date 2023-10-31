using Inventory.Domain.Entities;

namespace Inventory.Models
{
    public class PurchaseOrderModel
    {
        public Guid Id { get; set; }
        public string? SupplierId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? ProductRate { get; set; }
        public string? VariantId { get; set; }
        public int? Quantity { get; set; }
        public DateOnly? Date { get; set; }
        public DateOnly? DueDate { get; set; }
        public OrderStatus? OrderStatus { get; set; }
        public string? Remarks { get; set; }
        public bool IsChecked { get; set; }
    }
}
