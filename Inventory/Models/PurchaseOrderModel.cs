using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Inventory.Models
{
    public class PurchaseOrderModel
    {
        public Guid Id { get; set; }
        public string? SupplierId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? ProductRate { get; set; } = default(decimal);
        public string? VariantId { get; set; } = default;
        public int? Quantity { get; set; } = default(int);
        public DateOnly? Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public DateOnly? DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public OrderStatus OrderStatus { get; set; }
        public string? Remarks { get; set; } = default;
        public bool IsChecked { get; set; } = default;

    }


}
