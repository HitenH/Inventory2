using Inventory.Domain.Entities;
using Inventory.Models;

namespace Inventory.Service;

/// <summary>Which screen opened the "Bulk Entry" pop-up (owner's request, 13 Nov 2026).</summary>
public enum BulkEntryKind
{
    /// <summary>Purchase Orders: the pop-up also asks for supplier, dates and status (each row is its own order).</summary>
    PurchaseOrder,
    /// <summary>Lines of a purchase voucher (supplier already chosen on the voucher).</summary>
    Purchase,
    /// <summary>Lines of a sales order (customer already chosen on the voucher).</summary>
    SalesOrder,
}

/// <summary>One row typed in the Bulk Entry pop-up, before it is saved.</summary>
public sealed class BulkEntryRow
{
    public Guid ProductEntityId { get; set; }
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public Guid VariantEntityId { get; set; }
    /// <summary>The variant's own code (what the line lists show).</summary>
    public string VariantId { get; set; } = "";
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal? Rate { get; set; }
    /// <summary>Discount in percent (Purchase and Sales Order lines only).</summary>
    public int? DiscountPercent { get; set; }
    public string? Remarks { get; set; }

    public decimal Amount => LineAmounts.ComputeAmount(Rate, Quantity);
    public decimal? AmountAfterDiscount => LineAmounts.ComputeAmountAfterDiscount(Amount, DiscountPercent);
}

/// <summary>The supplier, dates and status that every row of a bulk purchase order gets.</summary>
public sealed class BulkOrderHeader
{
    public string? SupplierId { get; set; }
    public DateOnly? Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly? DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Open;
}

/// <summary>What the pop-up hands back when the operator presses "Save all".</summary>
public sealed class BulkEntryResult
{
    public List<BulkEntryRow> Rows { get; init; } = new();
    public BulkOrderHeader Header { get; init; } = new();
}

/// <summary>The rules and conversions of the Bulk Entry pop-up (kept out of the screen so they can be tested).</summary>
public static class BulkEntry
{
    /// <summary>Why a row cannot be added yet, or null when it is complete.</summary>
    public static string? WhatIsMissing(Guid productEntityId, Guid? variantEntityId, int? quantity)
    {
        if (productEntityId == Guid.Empty)
            return "Please select a product.";
        if (variantEntityId is null || variantEntityId == Guid.Empty)
            return "Please select a variant.";
        if (quantity is null or <= 0)
            return "Please enter a quantity.";
        return null;
    }

    /// <summary>Why the whole list cannot be saved yet, or null when it can.</summary>
    public static string? WhatIsMissingToSave(BulkEntryKind kind, BulkOrderHeader header, IReadOnlyCollection<BulkEntryRow> rows)
    {
        if (kind == BulkEntryKind.PurchaseOrder && string.IsNullOrWhiteSpace(header.SupplierId))
            return "Please select a supplier.";
        if (rows.Count == 0)
            return "There are no rows to save yet.";
        return null;
    }

    /// <summary>Rows as voucher lines, numbered on from <paramref name="firstSerialNumber"/> in the order they were typed.</summary>
    public static List<LineEditorModel> ToLines(IEnumerable<BulkEntryRow> rows, int firstSerialNumber, bool withRemarks)
    {
        var serial = firstSerialNumber;
        return rows.Select(r => new LineEditorModel
        {
            SerialNumber = serial++,
            ProductEntityId = r.ProductEntityId,
            ProductId = r.ProductId,
            ProductName = r.ProductName,
            VariantEntityId = r.VariantEntityId,
            ProductVariantId = r.VariantId,
            Quantity = r.Quantity,
            ProductRate = r.Rate,
            Amount = r.Amount,
            DiscountPercent = r.DiscountPercent,
            AmountAfterDiscount = r.AmountAfterDiscount,
            Remarks = withRemarks ? r.Remarks : null,
        }).ToList();
    }

    /// <summary>Rows as purchase orders: one order per row, all with the same supplier, dates and status.</summary>
    public static List<PurchaseOrderEntity> ToPurchaseOrders(IEnumerable<BulkEntryRow> rows, BulkOrderHeader header)
        => rows.Select(r => new PurchaseOrderEntity
        {
            SupplierId = header.SupplierId,
            Date = header.Date,
            DueDate = header.DueDate,
            OrderStatus = header.OrderStatus,
            ProductId = r.ProductId,
            ProductName = r.ProductName,
            // Purchase orders store the variant's code (as the Purchase Orders screen does).
            VariantId = r.VariantId,
            Quantity = r.Quantity,
            ProductRate = r.Rate,
            Remarks = r.Remarks,
        }).ToList();
}
