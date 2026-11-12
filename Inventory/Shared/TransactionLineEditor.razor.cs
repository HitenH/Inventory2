using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Inventory.Shared
{
    /// <summary>
    /// The line editor (form + list) on the Purchase and Sales Order screens (batch 12; one copy instead of two).
    /// What differs per voucher type lives in <see cref="TransactionLines"/>.
    /// </summary>
    public partial class TransactionLineEditor
    {
        [Parameter, EditorRequired] public TransactionLines Lines { get; set; } = default!;
        [Parameter] public EventCallback<bool> ChangeState { get; set; }
        [Inject] private IProductRepository ProductRepository { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private ILogger<TransactionLineEditor> Logger { get; set; } = default!;

        private ProductEntity product = new();
        private ProductModel selectedProduct;
        private int serialnumber = 1;

        private LineEditorModel line = new();
        private List<LineEditorModel> lines = new();

        protected override void OnParametersSet()
        {
            lines = Lines.Load();
            if (lines.Count != 0)
            {
                serialnumber = lines.Max(l => l.SerialNumber) + 1;
            }
        }

        public async Task AddLine()
        {
            if (Lines.HeaderId == Guid.Empty || product?.Id == Guid.Empty || line?.VariantEntityId is null)
            {
                Snackbar.Add("Select a product and variant before adding a line.", Severity.Warning);
                return;
            }

            try
            {
                line.SerialNumber = serialnumber;
                await Lines.Create(line, product.Id, serialnumber);

                CancelLine();
                serialnumber += 1;
                await ChangeState.InvokeAsync(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Add {Kind} line failed", Lines.Kind);
                Snackbar.Add($"Could not add line: {ex.Message}", Severity.Error);
            }
        }

        public async Task EditLine()
        {
            if (line?.Id == Guid.Empty || product?.Id == Guid.Empty || line.VariantEntityId is null)
            {
                Snackbar.Add("Select a product and variant before saving.", Severity.Warning);
                return;
            }

            try
            {
                await Lines.Update(line, product.Id);

                CancelLine();
                await ChangeState.InvokeAsync(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Update {Kind} line failed", Lines.Kind);
                Snackbar.Add($"Could not update line: {ex.Message}", Severity.Error);
            }
        }

        public async Task DeleteLine(Guid id)
        {
            try
            {
                await Lines.Delete(id);
                await ChangeState.InvokeAsync(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete {Kind} line failed", Lines.Kind);
                Snackbar.Add($"Could not delete line: {ex.Message}", Severity.Error);
            }
        }

        public void CancelLine()
        {
            selectedProduct = new();
            line = new();
            product = new();
        }

        public void QuantityChanged(int? value)
        {
            line.Quantity = value;
            GetAmount();
        }

        public void RateChanged(decimal? value)
        {
            line.ProductRate = value;
            GetAmount();
        }

        public void GetAmount()
        {
            line.Amount = LineAmounts.ComputeAmount(line.ProductRate, line.Quantity);
            line.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(line.Amount, line.Discount);
        }

        private void DiscountChanged(int? value)
        {
            line.Discount = value;
            line.AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(line.Amount, line.Discount);
        }

        public void EditLineClicked(LineEditorModel model)
        {
            CancelLine();
            line = model;
            serialnumber = model.SerialNumber;
            product = model.Product;
        }

        private readonly ColumnSorter<LineEditorModel> sorter = new(
            new Dictionary<string, Func<LineEditorModel, object?>>
            {
                { "SerialNumber", c => c.SerialNumber },
                { "ProductId", c => c.ProductId },
                { "ProductName", c => c.ProductName },
                { "VariantId", c => c.ProductVariantId },
                { "Quantity", c => c.Quantity },
                { "ProductRate", c => c.ProductRate },
                { "Amount", c => c.Amount },
                { "Discount", c => c.Discount },
                { "AmmountAfterDiscount", c => c.AmountAfterDiscount },
                { "Remarks", c => c.Remarks },
            });

        public void SortItem(string column)
        {
            lines = sorter.Sort(lines, column);
        }

        private async Task OnProductSelected(ProductModel? _product)
        {
            if (_product != null)
            {
                selectedProduct = _product;
                product = await ProductRepository.GetProductWithVariantsById(_product.Id);
                line.ProductId = _product.ProductId;
                line.ProductName = _product.Name;
            }
        }
    }
}
