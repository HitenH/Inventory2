using Inventory.Models;
using MudBlazor;

namespace Inventory.Shared.Selectors
{
    /// <summary>What the bottom bar of a picker pop-up shows.</summary>
    public enum PickerActionsBar { None, Empty, Cancel }

    /// <summary>
    /// Everything that differs between the customer, supplier and product pickers (batch 12):
    /// texts, which fields are searched, and the small differences in how each pop-up looks.
    /// The search box (<see cref="LookupAutocomplete{TItem}"/>) and pop-up (LookupDialog) are shared.
    /// </summary>
    public sealed class LookupSettings<TItem>
    {
        public required Func<TItem, string?> IdOf { get; init; }
        public required Func<TItem, string?> NameOf { get; init; }

        // Search box
        public required string Placeholder { get; init; }
        public required Func<TItem, IEnumerable<string?>> SearchBoxFields { get; init; }
        public required string SearchBoxLoadFailedLog { get; init; }
        public required string SearchBoxLoadFailedMessage { get; init; }
        public required Severity SearchBoxLoadFailedSeverity { get; init; }

        // Pop-up
        public required string DialogTitle { get; init; }
        public required string IdHeader { get; init; }
        public required string NameHeader { get; init; }
        public required string ActionHeader { get; init; }
        public string? ActionDataLabel { get; init; }
        public required Func<TItem, IEnumerable<string?>> DialogFields { get; init; }
        public required string DialogLoadFailedLog { get; init; }
        public string DialogLoadFailedMessage { get; init; } = "Something went wrong";
        public required Severity DialogLoadFailedSeverity { get; init; }
        public bool Dense { get; init; }
        public bool Hover { get; init; }
        public bool Striped { get; init; }
        public bool SortableHeaders { get; init; }
        public string? TableClass { get; init; }
        public Typo NotFoundTypo { get; init; } = Typo.h6;
        public PickerActionsBar ActionsBar { get; init; } = PickerActionsBar.None;
    }

    /// <summary>The three pickers. Kept exactly as each looked and searched before they were merged.</summary>
    public static class Lookups
    {
        public static readonly LookupSettings<CustomerModel> Customer = new()
        {
            IdOf = c => c.CustomerId,
            NameOf = c => c.Name,
            Placeholder = "Search a customer...",
            SearchBoxFields = c => new[] { c.Name, c.CustomerId, c.Area, c.Address },
            SearchBoxLoadFailedLog = "Customer search: loading customers failed",
            SearchBoxLoadFailedMessage = "Something went wrong",
            SearchBoxLoadFailedSeverity = Severity.Warning,
            DialogTitle = "Customer Dialog",
            IdHeader = "Customer ID",
            NameHeader = "Customer Name",
            ActionHeader = "Actions",
            ActionDataLabel = "Actions",
            // The customer pop-up also finds customers by phone number.
            DialogFields = c => new[] { c.CustomerId, c.Name, c.Area }.Concat(c.Mobiles.Select(m => m.Phone)),
            DialogLoadFailedLog = "Customer picker: loading customers failed",
            DialogLoadFailedSeverity = Severity.Warning,
            Dense = true,
            Hover = true,
            NotFoundTypo = Typo.subtitle1,
            ActionsBar = PickerActionsBar.Empty,
        };

        public static readonly LookupSettings<SupplierModel> Supplier = new()
        {
            IdOf = s => s.SupplierId,
            NameOf = s => s.Name,
            Placeholder = "Search a supplier...",
            SearchBoxFields = s => new[] { s.SupplierId, s.Name },
            SearchBoxLoadFailedLog = "Supplier search: loading suppliers failed",
            SearchBoxLoadFailedMessage = "Something went wrong while getting suppliers.",
            SearchBoxLoadFailedSeverity = Severity.Error,
            DialogTitle = "Suppliers List",
            IdHeader = "Supplier ID",
            NameHeader = "Supplier Name",
            ActionHeader = "Actions",
            ActionDataLabel = "Actions",
            DialogFields = s => new[] { s.SupplierId, s.Name },
            DialogLoadFailedLog = "Supplier picker: loading suppliers failed",
            DialogLoadFailedSeverity = Severity.Warning,
            Striped = true,
            SortableHeaders = true,
            ActionsBar = PickerActionsBar.Cancel,
        };

        public static readonly LookupSettings<ProductModel> Product = new()
        {
            IdOf = p => p.ProductId,
            NameOf = p => p.Name,
            Placeholder = "Search a product...",
            SearchBoxFields = p => new[] { p.ProductId, p.Name },
            SearchBoxLoadFailedLog = "Product search: loading products failed",
            SearchBoxLoadFailedMessage = "Something went wrong while getting products.",
            SearchBoxLoadFailedSeverity = Severity.Error,
            DialogTitle = "Products List",
            IdHeader = "Product ID",
            NameHeader = "Product Name",
            ActionHeader = "Action",
            DialogFields = p => new[] { p.ProductId, p.Name },
            DialogLoadFailedLog = "Product picker: loading products failed",
            DialogLoadFailedSeverity = Severity.Error,
            Striped = true,
            TableClass = "my-mudtable",
        };

        /// <summary>What the search box lists for the typed text: every item whose search-box fields match (no limit).</summary>
        public static IEnumerable<TItem> SearchBoxMatches<TItem>(IEnumerable<TItem> items, LookupSettings<TItem> settings, string? search)
            => items.Where(i => i is not null && Matches(settings.SearchBoxFields(i), search)).ToList();

        /// <summary>
        /// Up/Down in the pop-up: the row <paramref name="step"/> places from <paramref name="current"/>,
        /// stopping at the first and last row (no wrap-around, so holding a key never jumps to the other end).
        /// </summary>
        public static TItem? MoveHighlight<TItem>(IList<TItem> rows, TItem? current, int step)
        {
            if (rows.Count == 0)
            {
                return default;
            }
            var index = current is null ? -1 : rows.IndexOf(current);
            if (index < 0)
            {
                return rows[0];
            }
            return rows[Math.Clamp(index + step, 0, rows.Count - 1)];
        }

        /// <summary>
        /// What the search box shows for the item a page has chosen: the item itself, or nothing when the page
        /// passes no item or an empty one (pages use new() for "nothing chosen").
        /// </summary>
        public static TItem? ShownSelection<TItem>(TItem? chosen, LookupSettings<TItem> settings)
            => chosen is null || string.IsNullOrWhiteSpace(settings.NameOf(chosen)) ? default : chosen;

        /// <summary>Case-insensitive "contains" on any of the fields; empty fields never match; an empty search matches all.</summary>
        public static bool Matches(IEnumerable<string?> fields, string? search)
        {
            var term = search?.Trim();
            if (string.IsNullOrEmpty(term))
            {
                return true;
            }

            return fields.Any(f => !string.IsNullOrEmpty(f) && f.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }
}
