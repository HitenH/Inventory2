using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class SalesOrderModel
    {
        public Guid Id { get; set; }
        public int VoucherId { get; set; }
        [Required]
        public string CustomerId { get; set; }
        [Required]
        public string CustomerName { get; set; }
        [Required]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        [Required]
        public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public decimal? TotalAmountProduct { get; set; }
        public OrderStatus OrderStatus { get; set; }
    }
}
