using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class SalesModel
    {
        public Guid Id { get; set; }
        [Required]
        public int VoucherId { get; set; }
        [Required]
        public string CustomerId { get; set; }
        [Required]
        public string CustomerName { get; set; }
        [Required]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public List<SalesVariantEntity>? SalesVariants { get; set; } = new();
    }
}
