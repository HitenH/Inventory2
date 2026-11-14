namespace Inventory.Domain.Entities;

public class SalesSummaryEntity
{
    public Guid Id { get; set; }
    public int SerialNumber { get; set; }
    public Guid ProductEntityId { get; set; }
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal ProductRate { get; set; }
    public decimal Amount { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.Column("Discount")]
    public int DiscountPercent { get; set; }
    public decimal AmountAfterDiscount { get; set; }
    public Guid SalesEntityId { get; set; }
    public SalesEntity Sale { get; set; }
}
