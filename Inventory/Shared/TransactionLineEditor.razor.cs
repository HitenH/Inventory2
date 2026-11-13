using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private IDialogService DialogService { get; set; } = default!;

        /// <summary>After a line is saved the cursor goes back to the product box, ready for the next item (owner's request).</summary>
        private Task FocusProductAsync() => JS.FocusBoxAsync(".focus-line-product");

        /// <summary>Enter on a chosen product: the cursor moves to the Variant ID drop-down.</summary>
        private Task OnProductEnter(ProductModel _) => JS.FocusBoxAsync("#variantid");

        // The product being entered: its ID and (after picking it) its variants for the drop-down.
        private ProductModel product = new();
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
                await FocusProductAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Add {Kind} line failed", Lines.Kind);
                Snackbar.Add($"Could not add line: {ex.Message}", Severity.Error);
            }
        }

        /// <summary>
        /// Bulk Entry pop-up (owner's request, Nov 2026): one product, many variants, product can be changed;
        /// all rows are added to this voucher together (all or nothing), numbered after the existing lines.
        /// </summary>
        private async Task OpenBulkEntryAsync()
        {
            if (Lines.HeaderId == Guid.Empty)
            {
                Snackbar.Add(Lines.BulkKind == BulkEntryKind.Purchase
                    ? "Please select a supplier first." : "Please select a customer first.", Severity.Warning);
                return;
            }
            var result = await Inventory.MudBlazorComponents.BulkEntryDialog.ShowAsync(DialogService, Lines.BulkKind);
            if (result == null)
            {
                await FocusProductAsync();
                return;
            }
            try
            {
                var first = lines.Count == 0 ? 1 : lines.Max(l => l.SerialNumber) + 1;
                await Lines.CreateRange(BulkEntry.ToLines(result.Rows, first, Lines.HasRemarks));
                Snackbar.Add(result.Rows.Count == 1 ? "Added 1 line." : $"Added {result.Rows.Count} lines.", Severity.Success);
                CancelLine();
                serialnumber = first + result.Rows.Count;
                await ChangeState.InvokeAsync(true);
                await FocusProductAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Bulk entry: adding {Kind} lines failed", Lines.Kind);
                Snackbar.Add("Could not add the lines. Nothing was saved. " + (DbErrors.RuleMessage(ex) ?? ex.Message), Severity.Error);
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
                await FocusProductAsync();
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

        public async Task EditLineClicked(LineEditorModel model)
        {
            CancelLine();
            line = model;
            serialnumber = model.SerialNumber;
            // Batch 15: show the line's product name in the search box and its variants in the drop-down
            // (by Variant ID, current one chosen). Used to show an empty box and the variant's internal code.
            product = new ProductModel { Id = model.ProductEntityId };
            selectedProduct = new ProductModel { Id = model.ProductEntityId, ProductId = model.ProductId, Name = model.ProductName };
            try
            {
                var loaded = await ProductRepository.GetProductWithVariantsById(model.ProductEntityId);
                if (loaded != null && loaded.Id != Guid.Empty)
                {
                    product = loaded.ToModel();
                }
            }
            catch (Exception ex)
            {
                // The line can still be edited; only the variant drop-down is missing.
                Logger.LogError(ex, "Loading the product of a {Kind} line failed", Lines.Kind);
            }
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
                product = (await ProductRepository.GetProductWithVariantsById(_product.Id)).ToModel();
                line.ProductId = _product.ProductId;
                line.ProductName = _product.Name;
                // A variant of the previously chosen product does not belong to this one: make the user pick again
                // (otherwise the save is refused by the "variant must belong to the product" rule).
                if (line.VariantEntityId is Guid v && !product.Variants.Any(x => x.Id == v))
                {
                    line.VariantEntityId = null;
                    line.ProductVariantId = "";
                }
            }
        }
    }
}
