namespace Inventory.Models;



public class ProductReportItem
{
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string? VariantId { get; set; }
    public string VariantName { get; set; }
    /// <summary>Stage 2: sellable pieces only (damaged are counted separately).</summary>
    public int StockInHand { get; set; }
    /// <summary>Stage 2: pieces lying in damage.</summary>
    public int Damaged { get; set; }
    /// <summary>Stage 2: pieces on stock changes waiting for approval (not moved yet).</summary>
    public int WaitingApproval { get; set; }
    public int? ReorderLevel { get; set; }
    public decimal? SalesAmount { get; set; }
    public int? SalesQuantity { get; set; }
    public string CategoryName { get; set; }
}
