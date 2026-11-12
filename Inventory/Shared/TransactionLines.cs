using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;

namespace Inventory.Shared
{
    /// <summary>
    /// What the shared line editor needs to know about the voucher it edits (batch 12):
    /// where its lines are, and how to save them. One small class per voucher type.
    /// </summary>
    public abstract class TransactionLines
    {
        /// <summary>Used in log messages, e.g. "purchase" or "sales order".</summary>
        public abstract string Kind { get; }

        /// <summary>True when the lines have a Remarks column (sales orders).</summary>
        public abstract bool HasRemarks { get; }

        public abstract Guid HeaderId { get; }

        /// <summary>The voucher's lines in serial-number order.</summary>
        public abstract List<LineEditorModel> Load();

        public abstract Task Create(LineEditorModel line, Guid productEntityId, int serialNumber);

        public abstract Task Update(LineEditorModel line, Guid productEntityId);

        public abstract Task Delete(Guid lineId);
    }

    public sealed class PurchaseLines : TransactionLines
    {
        private readonly PurchaseEntity purchase;
        private readonly IPurchaseVariantRepository repository;

        public PurchaseLines(PurchaseEntity purchase, IPurchaseVariantRepository repository)
        {
            this.purchase = purchase;
            this.repository = repository;
        }

        public override string Kind => "purchase";
        public override bool HasRemarks => false;
        public override Guid HeaderId => purchase.Id;

        public override List<LineEditorModel> Load()
        {
            purchase.PurchaseVariants ??= new List<PurchaseVariant>();
            purchase.PurchaseVariants = [.. purchase.PurchaseVariants.OrderBy(o => o.SerialNumber)];
            return purchase.PurchaseVariants.Select(v => new LineEditorModel
            {
                Amount = v.Amount,
                AmountAfterDiscount = v.AmountAfterDiscount,
                Discount = v.Discount,
                Id = v.Id,
                ProductId = v.Product?.ProductId ?? "",
                ProductName = v.Product?.Name ?? "",
                ProductRate = v.ProductRate,
                Quantity = v.Quantity,
                SerialNumber = v.SerialNumber,
                VariantEntityId = v.VariantEntityId,
                ProductVariantId = v.ProductVariant?.VariantId ?? "",
                ProductEntityId = v.ProductEntityId,
                Product = v.Product
            }).ToList();
        }

        public override Task Create(LineEditorModel line, Guid productEntityId, int serialNumber) =>
            repository.Create(new PurchaseVariant
            {
                PurchaseEntityId = purchase.Id,
                ProductEntityId = productEntityId,
                VariantEntityId = line.VariantEntityId!.Value,
                SerialNumber = serialNumber,
                Quantity = line.Quantity,
                Amount = line.Amount,
                Discount = line.Discount,
                AmountAfterDiscount = line.AmountAfterDiscount,
                ProductRate = line.ProductRate
            });

        public override Task Update(LineEditorModel line, Guid productEntityId) =>
            repository.Update(new PurchaseVariant
            {
                Id = line.Id,
                PurchaseEntityId = purchase.Id,
                ProductEntityId = productEntityId,
                VariantEntityId = line.VariantEntityId!.Value,
                SerialNumber = line.SerialNumber,
                Quantity = line.Quantity,
                Amount = line.Amount,
                Discount = line.Discount,
                AmountAfterDiscount = line.AmountAfterDiscount,
                ProductRate = line.ProductRate
            });

        public override Task Delete(Guid lineId) => repository.Delete(new PurchaseVariant { Id = lineId });
    }

    public sealed class SalesOrderLines : TransactionLines
    {
        private readonly SalesOrderEntity salesOrder;
        private readonly ISalesOrderVariantRepository repository;

        public SalesOrderLines(SalesOrderEntity salesOrder, ISalesOrderVariantRepository repository)
        {
            this.salesOrder = salesOrder;
            this.repository = repository;
        }

        public override string Kind => "sales order";
        public override bool HasRemarks => true;
        public override Guid HeaderId => salesOrder.Id;

        public override List<LineEditorModel> Load()
        {
            salesOrder.SalesOrderVariants ??= new List<SalesOrderVariantEntity>();
            salesOrder.SalesOrderVariants = [.. salesOrder.SalesOrderVariants.OrderBy(o => o.SerialNumber)];
            return salesOrder.SalesOrderVariants.Select(v => new LineEditorModel
            {
                Amount = v.Amount,
                AmountAfterDiscount = v.AmountAfterDiscount,
                Discount = v.Discount,
                Id = v.Id,
                ProductId = v.Product?.ProductId ?? "",
                ProductName = v.Product?.Name ?? "",
                ProductRate = v.ProductRate,
                Quantity = v.Quantity,
                SerialNumber = v.SerialNumber,
                Remarks = v.Remarks,
                VariantEntityId = v.VariantEntityId,
                ProductVariantId = v.ProductVariant?.VariantId ?? "",
                ProductEntityId = v.ProductEntityId,
                Product = v.Product
            }).ToList();
        }

        public override Task Create(LineEditorModel line, Guid productEntityId, int serialNumber) =>
            repository.Create(new SalesOrderVariantEntity
            {
                SalesOrderEntityId = salesOrder.Id,
                ProductEntityId = productEntityId,
                VariantEntityId = line.VariantEntityId!.Value,
                SerialNumber = serialNumber,
                Quantity = line.Quantity,
                ProductRate = line.ProductRate,
                Amount = line.Amount,
                Discount = line.Discount,
                AmountAfterDiscount = line.AmountAfterDiscount,
                Remarks = line.Remarks
            });

        public override Task Update(LineEditorModel line, Guid productEntityId) =>
            repository.Update(new SalesOrderVariantEntity
            {
                Id = line.Id,
                SalesOrderEntityId = salesOrder.Id,
                ProductEntityId = productEntityId,
                VariantEntityId = line.VariantEntityId!.Value,
                SerialNumber = line.SerialNumber,
                Quantity = line.Quantity,
                ProductRate = line.ProductRate,
                Amount = line.Amount,
                Discount = line.Discount,
                AmountAfterDiscount = line.AmountAfterDiscount,
                Remarks = line.Remarks
            });

        public override async Task Delete(Guid lineId)
        {
            var line = await repository.GetById(lineId);
            if (line != null)
            {
                await repository.Delete(line);
            }
        }
    }
}
