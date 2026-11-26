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

        /// <summary>Which kind of Bulk Entry pop-up this voucher uses.</summary>
        public abstract Inventory.Service.BulkEntryKind BulkKind { get; }

        public abstract Guid HeaderId { get; }

        /// <summary>The voucher's lines in serial-number order.</summary>
        public abstract List<LineEditorModel> Load();

        public abstract Task Create(LineEditorModel line, Guid productEntityId, int serialNumber);

        public abstract Task Update(LineEditorModel line, Guid productEntityId);

        /// <summary>Bulk entry: adds all the lines (each with its own product and serial number) in one go, all or nothing.</summary>
        public abstract Task CreateRange(List<LineEditorModel> newLines);

        public abstract Task Delete(Guid lineId);

        /// <summary>Stage 2: false for vouchers that carry quantities only (sales returns): no rate, amount or discount boxes.</summary>
        public virtual bool HasRates => true;

        /// <summary>Stage 2: a rule of this voucher type on top of the common line rules (e.g. a return must be more than 0), or null.</summary>
        public virtual string? ExtraProblem(LineEditorModel line) => null;
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
        public override Inventory.Service.BulkEntryKind BulkKind => Inventory.Service.BulkEntryKind.Purchase;
        public override Guid HeaderId => purchase.Id;

        public override List<LineEditorModel> Load()
        {
            purchase.PurchaseVariants ??= new List<PurchaseVariant>();
            purchase.PurchaseVariants = [.. purchase.PurchaseVariants.OrderBy(o => o.SerialNumber)];
            return purchase.PurchaseVariants.Select(v => new LineEditorModel
            {
                Amount = v.Amount,
                AmountAfterDiscount = v.AmountAfterDiscount,
                DiscountPercent = v.DiscountPercent,
                Id = v.Id,
                ProductId = v.Product?.ProductId ?? "",
                ProductName = v.Product?.Name ?? "",
                ProductRate = v.ProductRate,
                Quantity = v.Quantity,
                SerialNumber = v.SerialNumber,
                VariantEntityId = v.VariantEntityId,
                ProductVariantId = v.ProductVariant?.VariantId ?? "",
                ProductEntityId = v.ProductEntityId,
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
                DiscountPercent = line.DiscountPercent,
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
                DiscountPercent = line.DiscountPercent,
                AmountAfterDiscount = line.AmountAfterDiscount,
                ProductRate = line.ProductRate
            });

        public override Task Delete(Guid lineId) => repository.Delete(new PurchaseVariant { Id = lineId });

        public override Task CreateRange(List<LineEditorModel> newLines) =>
            repository.CreateRange(newLines.Select(line => new PurchaseVariant
            {
                PurchaseEntityId = purchase.Id,
                ProductEntityId = line.ProductEntityId,
                VariantEntityId = line.VariantEntityId!.Value,
                SerialNumber = line.SerialNumber,
                Quantity = line.Quantity,
                Amount = line.Amount,
                DiscountPercent = line.DiscountPercent,
                AmountAfterDiscount = line.AmountAfterDiscount,
                ProductRate = line.ProductRate
            }).ToList());
    }

    /// <summary>
    /// Stage 2, batch 2-4: the lines of a sales return (quantities only). Saved through <see cref="Inventory.Service.StockChangeService"/>,
    /// which also refuses changes that would not match the damage already recorded on a line.
    /// </summary>
    public sealed class SalesReturnLines : TransactionLines
    {
        private readonly StockChangeEntity salesReturn;
        private readonly Inventory.Service.StockChangeService service;

        public SalesReturnLines(StockChangeEntity salesReturn, Inventory.Service.StockChangeService service)
        {
            this.salesReturn = salesReturn;
            this.service = service;
        }

        public override string Kind => "sales return";
        public override bool HasRemarks => false;
        public override bool HasRates => false;
        // No Bulk Entry on sales returns (no button); the kind only matters for rates, which a return does not have.
        public override Inventory.Service.BulkEntryKind BulkKind => Inventory.Service.BulkEntryKind.SalesOrder;
        public override Guid HeaderId => salesReturn.Id;

        public override string? ExtraProblem(LineEditorModel line) => line.Quantity is null or <= 0 ? "The quantity returned must be more than 0." : null;

        public override List<LineEditorModel> Load() =>
            salesReturn.Lines
                .Where(l => l.From == StockState.Outside)
                .OrderBy(l => l.SerialNumber)
                .Select(l => new LineEditorModel
                {
                    Id = l.Id,
                    SerialNumber = l.SerialNumber,
                    ProductEntityId = l.ProductEntityId,
                    ProductId = l.Product?.ProductId ?? "",
                    ProductName = l.Product?.Name ?? "",
                    VariantEntityId = l.VariantEntityId,
                    ProductVariantId = l.ProductVariant?.VariantId ?? "",
                    Quantity = l.Quantity,
                    ProductRate = null, Amount = null, DiscountPercent = null, AmountAfterDiscount = null,
                }).ToList();

        public override Task Create(LineEditorModel line, Guid productEntityId, int serialNumber) =>
            service.AddSalesReturnLineAsync(salesReturn.Id, productEntityId, line.VariantEntityId!.Value, line.Quantity, serialNumber);

        public override Task Update(LineEditorModel line, Guid productEntityId) =>
            service.UpdateSalesReturnLineAsync(line.Id, productEntityId, line.VariantEntityId!.Value, line.Quantity, line.SerialNumber);

        public override Task CreateRange(List<LineEditorModel> newLines) =>
            service.AddSalesReturnLinesAsync(salesReturn.Id, newLines.Select(l => (l.ProductEntityId, l.VariantEntityId!.Value, l.Quantity, l.SerialNumber)).ToList());

        public override Task Delete(Guid lineId) => service.DeleteSalesReturnLineAsync(lineId);
    }

    /// <summary>
    /// Stage 2, batch 2-6: good stock sent back to a supplier on a purchase return (quantities only). Lines that came from the
    /// damage report are listed apart on the screen and are not edited here.
    /// </summary>
    public sealed class PurchaseReturnLines : TransactionLines
    {
        private readonly StockChangeEntity purchaseReturn;
        private readonly Inventory.Service.DamagedGoodsService service;

        public PurchaseReturnLines(StockChangeEntity purchaseReturn, Inventory.Service.DamagedGoodsService service)
        {
            this.purchaseReturn = purchaseReturn;
            this.service = service;
        }

        public override string Kind => "purchase return";
        public override bool HasRemarks => false;
        public override bool HasRates => false;
        // No Bulk Entry button on purchase returns; the kind only matters for rates, which a return does not have.
        public override Inventory.Service.BulkEntryKind BulkKind => Inventory.Service.BulkEntryKind.Purchase;
        public override Guid HeaderId => purchaseReturn.Id;

        public override string? ExtraProblem(LineEditorModel line) => line.Quantity is null or <= 0 ? "The quantity sent back must be more than 0." : null;

        public override List<LineEditorModel> Load() =>
            purchaseReturn.Lines
                .Where(l => l.From == StockState.Sellable)
                .OrderBy(l => l.SerialNumber)
                .Select(l => new LineEditorModel
                {
                    Id = l.Id,
                    SerialNumber = l.SerialNumber,
                    ProductEntityId = l.ProductEntityId,
                    ProductId = l.Product?.ProductId ?? "",
                    ProductName = l.Product?.Name ?? "",
                    VariantEntityId = l.VariantEntityId,
                    ProductVariantId = l.ProductVariant?.VariantId ?? "",
                    Quantity = l.Quantity,
                    ProductRate = null, Amount = null, DiscountPercent = null, AmountAfterDiscount = null,
                }).ToList();

        public override Task Create(LineEditorModel line, Guid productEntityId, int serialNumber) =>
            service.AddPurchaseReturnLinesAsync(purchaseReturn.Id, [(productEntityId, line.VariantEntityId!.Value, line.Quantity, serialNumber)]);

        public override Task Update(LineEditorModel line, Guid productEntityId) =>
            service.UpdatePurchaseReturnLineAsync(line.Id, productEntityId, line.VariantEntityId!.Value, line.Quantity, line.SerialNumber);

        public override Task CreateRange(List<LineEditorModel> newLines) =>
            service.AddPurchaseReturnLinesAsync(purchaseReturn.Id, newLines.Select(l => (l.ProductEntityId, l.VariantEntityId!.Value, l.Quantity, l.SerialNumber)).ToList());

        public override Task Delete(Guid lineId) => service.DeletePurchaseReturnLineAsync(lineId);
    }

    /// <summary>
    /// Stage 2, batch 2-7: the lines of an entry on the Stock changes screen (quantities only; Remarks is the line's note).
    /// A count correction takes "3" for found more and "-2" for found fewer.
    /// </summary>
    public sealed class StockEntryLines : TransactionLines
    {
        private readonly StockChangeEntity entry;
        private readonly Inventory.Service.StockEntryType type;
        private readonly Inventory.Service.StockEntryService service;

        public StockEntryLines(StockChangeEntity entry, Inventory.Service.StockEntryType type, Inventory.Service.StockEntryService service)
        {
            this.entry = entry;
            this.type = type;
            this.service = service;
        }

        public override string Kind => "stock change";
        public override bool HasRemarks => true;
        public override bool HasRates => false;
        // No Bulk Entry button here; the kind only matters for rates, which a stock change does not have.
        public override Inventory.Service.BulkEntryKind BulkKind => Inventory.Service.BulkEntryKind.Purchase;
        public override Guid HeaderId => entry.Id;

        public override string? ExtraProblem(LineEditorModel line) => Inventory.Service.StockEntryService.QuantityProblem(type, line.Quantity);

        public override List<LineEditorModel> Load() =>
            entry.Lines
                .OrderBy(l => l.SerialNumber)
                .Select(l => new LineEditorModel
                {
                    Id = l.Id,
                    SerialNumber = l.SerialNumber,
                    ProductEntityId = l.ProductEntityId,
                    ProductId = l.Product?.ProductId ?? "",
                    ProductName = l.Product?.Name ?? "",
                    VariantEntityId = l.VariantEntityId,
                    ProductVariantId = l.ProductVariant?.VariantId ?? "",
                    Quantity = Inventory.Service.StockEntryService.Shown(l),
                    Remarks = l.Note,
                    ProductRate = null, Amount = null, DiscountPercent = null, AmountAfterDiscount = null,
                }).ToList();

        public override Task Create(LineEditorModel line, Guid productEntityId, int serialNumber) =>
            service.AddLinesAsync(entry.Id, [(productEntityId, line.VariantEntityId!.Value, line.Quantity, serialNumber, line.Remarks)]);

        public override Task Update(LineEditorModel line, Guid productEntityId) =>
            service.UpdateLineAsync(line.Id, productEntityId, line.VariantEntityId!.Value, line.Quantity, line.SerialNumber, line.Remarks);

        public override Task CreateRange(List<LineEditorModel> newLines) =>
            service.AddLinesAsync(entry.Id, newLines.Select(l => (l.ProductEntityId, l.VariantEntityId!.Value, l.Quantity, l.SerialNumber, l.Remarks)).ToList());

        public override Task Delete(Guid lineId) => service.DeleteLineAsync(lineId);
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
        public override Inventory.Service.BulkEntryKind BulkKind => Inventory.Service.BulkEntryKind.SalesOrder;
        public override Guid HeaderId => salesOrder.Id;

        public override List<LineEditorModel> Load()
        {
            salesOrder.SalesOrderVariants ??= new List<SalesOrderVariantEntity>();
            salesOrder.SalesOrderVariants = [.. salesOrder.SalesOrderVariants.OrderBy(o => o.SerialNumber)];
            return salesOrder.SalesOrderVariants.Select(v => new LineEditorModel
            {
                Amount = v.Amount,
                AmountAfterDiscount = v.AmountAfterDiscount,
                DiscountPercent = v.DiscountPercent,
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
                DiscountPercent = line.DiscountPercent,
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
                DiscountPercent = line.DiscountPercent,
                AmountAfterDiscount = line.AmountAfterDiscount,
                Remarks = line.Remarks
            });

        public override Task CreateRange(List<LineEditorModel> newLines) =>
            repository.CreateRange(newLines.Select(line => new SalesOrderVariantEntity
            {
                SalesOrderEntityId = salesOrder.Id,
                ProductEntityId = line.ProductEntityId,
                VariantEntityId = line.VariantEntityId!.Value,
                SerialNumber = line.SerialNumber,
                Quantity = line.Quantity,
                ProductRate = line.ProductRate,
                Amount = line.Amount,
                DiscountPercent = line.DiscountPercent,
                AmountAfterDiscount = line.AmountAfterDiscount,
                Remarks = line.Remarks
            }).ToList());

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
