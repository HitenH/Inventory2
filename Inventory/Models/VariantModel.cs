using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class VariantModel
    {
        public Guid Id { get; set; }
        [Required]
        public string? VariantId { get; set; }
        [Required]
        public string? Name { get; set; }
        public string? StockInHand { get; set; }
        public Image? Image { get; set; }
    }
}
