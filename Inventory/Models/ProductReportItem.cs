namespace Inventory.Models;



public class ProductReportItem
{
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string? VariantId { get; set; }
    public string VariantName { get; set; }
    public int StockInHand { get; set; }
    public int? ReorderLevel { get; set; }
    public decimal? SalesAmount { get; set; }
    public int? SalesQuantity { get; set; }
    public string CategoryName { get; set; }
}
