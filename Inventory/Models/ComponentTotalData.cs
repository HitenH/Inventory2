namespace Inventory.Models;

public class ComponentTotalData
{
    public decimal TotalQuantity { get; set; } = default(decimal);
    public decimal Discount { get; set; } = default(decimal);
    public decimal TotalAmount { get; set; } = default(decimal);
}
