using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    /// <summary>
    /// One line (product + variant + quantity + price) as shown in the shared line editor
    /// used by Purchase and Sales Order (batch 12; replaces PurchaseVariantModel and SalesOrderVariantModel).
    /// </summary>
    public class LineEditorModel
    {
        public Guid Id { get; set; }
        public int SerialNumber { get; set; }
        public Guid ProductEntityId { get; set; }
        [Required]
        public string ProductId { get; set; }
        [Required]
        public string ProductName { get; set; }
        [Required]
        public Guid? VariantEntityId { get; set; }
        public string ProductVariantId { get; set; }
        public int? Quantity { get; set; } = default(int);
        public decimal? ProductRate { get; set; } = default(decimal);
        public decimal? Amount { get; set; } = default(decimal);
        public int? Discount { get; set; } = default(int);
        public decimal? AmountAfterDiscount { get; set; } = default(decimal);
        /// <summary>Sales orders only.</summary>
        public string? Remarks { get; set; }

        /// <summary>A separate copy of this line, so the form can be cancelled without touching the table row.</summary>
        public LineEditorModel Clone() => (LineEditorModel)MemberwiseClone();
    }
}
