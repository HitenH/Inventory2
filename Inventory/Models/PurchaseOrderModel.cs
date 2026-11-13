using Inventory.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Inventory.Models
{
    public class PurchaseOrderModel
    {
        public Guid Id { get; set; }
        public string? SupplierId { get; set; }
        /// <summary>Shown in the list (owner's request: he knows suppliers by name). Not stored on the order.</summary>
        public string? SupplierName { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? ProductRate { get; set; } = default(decimal);
        public string? VariantId { get; set; } = default;
        public int? Quantity { get; set; } = default(int);
        public DateOnly? Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public DateOnly? DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Open; // new orders start as Open (owner's request)
        public string? Remarks { get; set; } = default;
        public bool IsChecked { get; set; } = default;

        /// <summary>A separate copy of this row (used as the backup while a table row is being edited).</summary>
        public PurchaseOrderModel Clone() => (PurchaseOrderModel)MemberwiseClone();

        /// <summary>Puts every value of <paramref name="other"/> back onto this row (keeps this object's identity).</summary>
        public void CopyFrom(PurchaseOrderModel other)
        {
            Id = other.Id;
            SupplierId = other.SupplierId;
            SupplierName = other.SupplierName;
            ProductId = other.ProductId;
            ProductName = other.ProductName;
            ProductRate = other.ProductRate;
            VariantId = other.VariantId;
            Quantity = other.Quantity;
            Date = other.Date;
            DueDate = other.DueDate;
            OrderStatus = other.OrderStatus;
            Remarks = other.Remarks;
            IsChecked = other.IsChecked;
        }

    }


}
