namespace Inventory.Service
{
    /// <summary>One screen that has a keyboard shortcut.</summary>
    /// <param name="Name">What the menu and the Dashboard call it.</param>
    /// <param name="Key">Alt + this letter opens <paramref name="ListUrl"/>.</param>
    /// <param name="ListUrl">The screen (usually a list) Alt + letter opens.</param>
    /// <param name="NewUrl">The empty "new" form Alt+Shift + letter and Alt+N open; null when the screen has none.</param>
    /// <param name="Pages">First part of every address that belongs to this screen (list, single record, summary), for Alt+N.</param>
    public sealed record ScreenShortcut(string Name, char Key, string ListUrl, string? NewUrl, string[] Pages);

    /// <summary>
    /// Keyboard shortcuts for moving around the app (owner's request, Nov 2026). One list used by the key handler,
    /// the menu hints and the Dashboard table, so they can never disagree.
    /// Avoided on purpose: Alt+F / Alt+E (Chrome menu), Alt+D (address bar); Alt+Tab never reaches the browser.
    /// </summary>
    public static class Shortcuts
    {
        /// <summary>Alt+N: a new record of the screen you are on.</summary>
        public const char NewForThisScreen = 'N';

        /// <summary>
        /// Alt+B: opens the screen's many-items pop-up (Bulk Entry on Purchase Orders / Purchases / Sales Orders,
        /// Scan Mass Products on Sale) by pressing its button (marked data-shortcut="bulk"). Handled in shortcuts.js.
        /// </summary>
        public const char BulkEntry = 'B';

        public static readonly IReadOnlyList<ScreenShortcut> Screens =
        [
            new("Dashboard", 'H', "/", null, [""]),
            new("Customers", 'C', "/customers", "/customer", ["customers", "customer"]),
            new("Suppliers", 'U', "/suppliers", "/supplier", ["suppliers", "supplier"]),
            new("Products", 'I', "/products", "/product", ["products", "product"]),
            new("Purchase Orders", 'Q', "/purchaseorder", null, ["purchaseorder"]),
            new("Purchases", 'P', "/purchases", "/purchase", ["purchases", "purchase"]),
            new("Sale Orders", 'O', "/salesorders", "/salesorder", ["salesorders", "salesorder"]),
            new("Sales", 'S', "/sales", "/sale", ["sales", "sale", "salessummary"]),
            new("Reports", 'R', "/reports", null, ["reports"]),
            new("Settings", 'G', "/settings", null, ["settings"]),
        ];

        /// <summary>Every letter the key handler should catch (all others, e.g. Alt+F for Chrome's menu, are left alone).</summary>
        public static IReadOnlyList<string> Letters =>
            Screens.Select(s => s.Key.ToString()).Append(NewForThisScreen.ToString()).Distinct().ToList();

        /// <summary>
        /// Where a shortcut leads from the current address, or null when it does nothing there
        /// (e.g. Alt+N on Reports, or Alt+Shift on a screen without a "new" form).
        /// </summary>
        public static string? Target(string currentPath, char key, bool shift)
        {
            key = char.ToUpperInvariant(key);
            if (key == NewForThisScreen)
            {
                var segment = FirstSegment(currentPath);
                return Screens.FirstOrDefault(s => s.Pages.Contains(segment, StringComparer.OrdinalIgnoreCase))?.NewUrl;
            }

            var screen = Screens.FirstOrDefault(s => s.Key == key);
            if (screen is null)
            {
                return null;
            }
            return shift ? screen.NewUrl : screen.ListUrl;
        }

        /// <summary>
        /// Where Esc leads (owner's request): from a single record (new customer, a purchase, a sale summary ...)
        /// back to its list; from a list or any other screen to the Dashboard; on the Dashboard and Login nothing.
        /// </summary>
        public static string? Back(string currentPath)
        {
            var segment = FirstSegment(currentPath);
            if (segment.Length == 0 || segment.Equals("login", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (segment.Equals("category", StringComparison.OrdinalIgnoreCase))
            {
                return "/products";   // categories are kept from the Products screen
            }

            var screen = Screens.FirstOrDefault(s => s.Pages.Contains(segment, StringComparer.OrdinalIgnoreCase));
            if (screen is null || segment.Equals(FirstSegment(screen.ListUrl), StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }
            return screen.ListUrl;
        }

        /// <summary>
        /// True when going from <paramref name="currentPath"/> to <paramref name="target"/> stays on the same screen
        /// (e.g. from a half-filled sale to a new sale). Such screens only load their data once, so the page is
        /// reloaded to start clean.
        /// </summary>
        public static bool NeedsReload(string currentPath, string target)
            => string.Equals(FirstSegment(currentPath), FirstSegment(target), StringComparison.OrdinalIgnoreCase);

        /// <summary>"sale" for "/sale/1234?x=1"; "" for the Dashboard.</summary>
        public static string FirstSegment(string path)
        {
            var p = path ?? "";
            var cut = p.IndexOfAny(['?', '#']);
            if (cut >= 0)
            {
                p = p[..cut];
            }
            return p.Trim('/').Split('/')[0];
        }

        /// <summary>Menu hint for the screen opened by <paramref name="listUrl"/>, e.g. "Alt+C".</summary>
        public static string? HintFor(string listUrl)
        {
            var screen = Screens.FirstOrDefault(s => s.ListUrl == listUrl);
            return screen is null ? null : $"Alt+{screen.Key}";
        }
    }
}
