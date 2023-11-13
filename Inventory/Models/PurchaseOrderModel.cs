using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Inventory.Models
{
    public class PurchaseOrderModel
    {
        public Guid Id { get; set; }
        [Required]
        public string? SupplierId { get; set; }
        [Required]
        public string? ProductId { get; set; }
        [Required]
        public string? ProductName { get; set; }
        public decimal? ProductRate { get; set; } = default(decimal);
        [Required]
        public string? VariantId { get; set; } = default;
        public int? Quantity { get; set; } = default(int);
        public DateOnly? Date { get; set; } = default;
        public DateOnly? DueDate { get; set; } = default;
        public OrderStatus? OrderStatus { get; set; }
        public string? Remarks { get; set; } = default;
        public bool IsChecked { get; set; } = default;

    }


}
