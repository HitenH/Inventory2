using Inventory.Models;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class SalesOrderEntity
    {
        [Key]
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        public Guid CustomerEntityId { get; set; }
        // No "= new()" here (batch 7): an automatic empty customer was inserted as a new blank customer
        // whenever a sales order was saved. The customer is set by loading it (Include) or left null.
        public CustomerEntity Customer { get; set; } = null!;
        public DateOnly Date { get; set; }
        public DateOnly DueDate { get; set; }
        public decimal? TotalAmountProduct { get; set; }
        /// <summary>Discount amount for the whole order (money, not %). Stored from batch 7; before that it was worked out backwards from the total.</summary>
    [System.ComponentModel.DataAnnotations.Schema.Column("Discount")]
        public decimal? DiscountAmount { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public List<SalesOrderVariantEntity>? SalesOrderVariants { get; set; } = new();
    }
}
