using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class SalesVariantModel
    {
        public Guid Id { get; set; }
        [Required]
        public string ProductId { get; set; }
        public Guid ProductEntityId { get; set; }
        /// <summary>The line's product with the variants that can be chosen when editing it.</summary>
        public ProductModel Product { get; set; }
        [Required]
        public Guid? VariantEntityId { get; set; }
        public string VariantId { get; set; }
        public int? Quantity { get; set; } = 1;
        public string ProductName { get; set; }
        public bool IsCreated { get; set; } = false;
        public int InnerId { get; set; }

        /// <summary>A separate copy of this row, so the edit form can be cancelled without touching the table row.</summary>
        public SalesVariantModel Clone() => (SalesVariantModel)MemberwiseClone();
    }
}
