using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class PurchaseModel
    {
        [Required]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        [Required]
        public string SupplierId { get; set; }
        [Required]
        public string SupplierName { get; set; }
        [Required]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public string? Remarks { get; set; } = String.Empty;
        public decimal? TotalAmountProduct { get; set; } = decimal.Zero;
    }
}
