using Inventory.Models;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class PurchaseOrderEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
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
