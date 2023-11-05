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
        public CustomerEntity Customer { get; set; } = new();
        public DateOnly Date { get; set; }
        public DateOnly DueDate { get; set; }
        public decimal? TotalAmountProduct { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public List<SalesOrderVariantEntity>? SalesOrderVariants { get; set; } = new();
    }
}
