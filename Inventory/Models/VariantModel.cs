using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class VariantModel
    {
        public Guid Id { get; set; }
        public string? VariantId { get; set; }
        public string? Name { get; set; }
        /// <summary>What the variant drop-downs show: "G – GANESH" (owner's request); just the ID when there is no separate name.</summary>
        public string DisplayText =>
            string.IsNullOrWhiteSpace(Name) || string.Equals(Name, VariantId, StringComparison.OrdinalIgnoreCase)
                ? VariantId ?? "" : $"{VariantId} – {Name}";
        public int? StockInHand { get; set; }
        public int? ReorderLevel { get; set; }
        public Image? Image { get; set; } = new();
    }
}
