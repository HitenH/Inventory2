namespace Inventory.Service;

/// <summary>One switch that a role can have, as shown on the Roles screen.</summary>
public sealed record PermissionInfo(string Key, string Label, string Group);

/// <summary>
/// Version 3.0, stage 1b: the list of switches a role can be given.
/// Keys are stored in the database as text, so never rename or reuse a key once it has shipped.
/// An Admin role has every switch, including ones added later, so a new key never locks the owner out.
/// </summary>
public static class Permissions
{
    public const string ScreenDashboard = "screen.dashboard";
    public const string ScreenCustomers = "screen.customers";
    public const string ScreenSuppliers = "screen.suppliers";
    public const string ScreenProducts = "screen.products";
    public const string ScreenPurchaseOrders = "screen.purchase-orders";
    public const string ScreenPurchases = "screen.purchases";
    public const string ScreenSalesOrders = "screen.sales-orders";
    public const string ScreenSales = "screen.sales";
    public const string ScreenReports = "screen.reports";
    /// <summary>Stage 2, batch 2-4: the Sales Returns list and a sales return.</summary>
    public const string ScreenSalesReturns = "screen.sales-returns";
    /// <summary>Stage 2, batch 2-6: the Purchase Returns list and a purchase return.</summary>
    public const string ScreenPurchaseReturns = "screen.purchase-returns";
    /// <summary>Stage 2, batch 2-5: the damage report (assign supplier, ready to send, repaired, written off, sent).</summary>
    public const string ScreenDamageReport = "screen.damage-report";
    /// <summary>Stage 2, batch 2-7: the Stock changes list and an entry (damage in the shop, count corrections, write-offs, opening stock).</summary>
    public const string ScreenStockChanges = "screen.stock-changes";

    public const string AdminUsers = "admin.users";
    public const string AdminImport = "admin.import";
    public const string AdminSettings = "admin.settings";
    /// <summary>Batch 1c-4: the "See activity log" button and (1c-5) the log screen. Rates in the log follow the rate switches.</summary>
    public const string ActivitySee = "activity.see";
    /// <summary>Stage L: the Labels screen (print product labels).</summary>
    public const string LabelsPrint = "labels.print";
    /// <summary>Stage L: add, change and delete the named label set-ups (sizes, margins) on the Label settings page.</summary>
    public const string LabelsSettings = "labels.settings";

    /// <summary>Stage 2, batch 2-3: the "Record damage" pop-up on a purchase and a sales return.</summary>
    public const string StockDamage = "stock.damage";
    /// <summary>Stage 2, batch 2-8: the "Waiting for approval" list - approve or reject stock changes.</summary>
    public const string StockApprove = "stock.approve";
    /// <summary>Stage 3, batch 3-2: set which racks a variant is kept on (product page; later put-away). Places and racks themselves are kept from Settings.</summary>
    public const string StockRacks = "stock.racks";
    /// <summary>Stage 3, batch 3-4 (Hiten, 26 Nov 2026: "maybe some senior can do it"): change a sale's "From" place (e.g. a bulk order straight from the godown).</summary>
    public const string SalesOtherPlace = "sales.other-place";
    /// <summary>
    /// Stage 3, batches 3-5/3-6 (Hiten, 26 Nov 2026: "Yes split. One person if they have permission can send and receive. But
    /// people with only 1 permission can do only 1 task"): send stock to another place (new transfer, starting count, "Send to
    /// shop" on a purchase) …
    /// </summary>
    public const string StockTransferSend = "stock.transfer-send";
    /// <summary>… and confirm what came in at the receiving place.</summary>
    public const string StockTransferReceive = "stock.transfer-receive";
    /// <summary>Not a switch: the Stock transfers screens open with either of the two above (<see cref="UserAccess.AllowsPath"/>).</summary>
    public const string StockTransfers = "stock.transfers";
    /// <summary>Stage 3, batch 3-6 (Hiten, 26 Nov 2026: "only for admin as of now"): write off pieces missing on a stock transfer. Admin by default; can be given to a role.</summary>
    public const string StockTransferWriteOff = "stock.transfer-writeoff";

    public const string RatesPurchaseSee = "rates.purchase.see";
    public const string RatesSaleSee = "rates.sale.see";

    public const string ScreensGroup = "Screens";
    public const string AdminGroup = "Admin";
    public const string RatesGroup = "Rates";
    public const string StockGroup = "Stock";

    /// <summary>Required by a screen nobody has listed below: only an Admin role can open it.</summary>
    public const string Unlisted = "unlisted";

    // First part of the address -> the switch needed to open it (batch 1b-4).
    private static readonly Dictionary<string, string> ScreenOf = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = ScreenDashboard,
        ["customers"] = ScreenCustomers, ["customer"] = ScreenCustomers,
        ["suppliers"] = ScreenSuppliers, ["supplier"] = ScreenSuppliers,
        ["products"] = ScreenProducts, ["product"] = ScreenProducts, ["category"] = ScreenProducts,
        ["purchaseorder"] = ScreenPurchaseOrders,
        ["purchases"] = ScreenPurchases, ["purchase"] = ScreenPurchases,
        ["salesorders"] = ScreenSalesOrders, ["salesorder"] = ScreenSalesOrders,
        ["sales"] = ScreenSales, ["sale"] = ScreenSales, ["salessummary"] = ScreenSales,
        ["salesreturns"] = ScreenSalesReturns, ["salesreturn"] = ScreenSalesReturns,
        ["purchasereturns"] = ScreenPurchaseReturns, ["purchasereturn"] = ScreenPurchaseReturns,
        ["damage"] = ScreenDamageReport,
        ["stockchanges"] = ScreenStockChanges, ["stockchange"] = ScreenStockChanges,
        ["approvals"] = StockApprove,
        ["reasons"] = AdminSettings,   // the reasons list is kept from Settings
        ["places"] = AdminSettings,    // stage 3: places and racks are kept from Settings
        ["transfers"] = StockTransfers, ["transfer"] = StockTransfers,   // stage 3, batch 3-5: Send or Receive
        ["reports"] = ScreenReports,
        ["settings"] = AdminSettings,
        ["users"] = AdminUsers, ["roles"] = AdminUsers,
        ["activity"] = ActivitySee,
        ["labels"] = LabelsPrint,
        ["labelsettings"] = LabelsSettings,
    };

    /// <summary>Screens every logged-in user may open, whatever their roles.</summary>
    private static readonly HashSet<string> OpenToEveryone = new(StringComparer.OrdinalIgnoreCase) { "login", "changepassword" };

    /// <summary>
    /// The switch needed to open an address such as "/sale/123?x=1", or null when everyone may open it.
    /// An address missing from the list needs <see cref="Unlisted"/>, so a new screen is closed until it is listed here.
    /// </summary>
    public static string? ForPath(string path)
    {
        var clean = path.Split('?', '#')[0].Trim('/');
        var first = clean.Split('/')[0];
        if (OpenToEveryone.Contains(first))
        {
            return null;
        }

        return ScreenOf.TryGetValue(first, out var key) ? key : Unlisted;
    }

    public static IReadOnlyList<PermissionInfo> All { get; } = new List<PermissionInfo>
    {
        new(ScreenDashboard, "Open the Dashboard", ScreensGroup),
        new(ScreenCustomers, "Open the Customers screen", ScreensGroup),
        new(ScreenSuppliers, "Open the Suppliers screen", ScreensGroup),
        new(ScreenProducts, "Open the Products screen", ScreensGroup),
        new(ScreenPurchaseOrders, "Open the Purchase Orders screen", ScreensGroup),
        new(ScreenPurchases, "Open the Purchases screen", ScreensGroup),
        new(ScreenSalesOrders, "Open the Sales Orders screen", ScreensGroup),
        new(ScreenSales, "Open the Sales screen", ScreensGroup),
        new(ScreenSalesReturns, "Open the Sales Returns screen", ScreensGroup),
        new(ScreenPurchaseReturns, "Open the Purchase Returns screen", ScreensGroup),
        new(ScreenDamageReport, "Open the Damage report", ScreensGroup),
        new(ScreenStockChanges, "Open the Stock changes screen", ScreensGroup),
        new(ScreenReports, "Open the Reports screen", ScreensGroup),
        new(LabelsPrint, "Print labels", ScreensGroup),
        new(AdminUsers, "Manage users and roles", AdminGroup),
        new(AdminImport, "Import data", AdminGroup),
        new(AdminSettings, "Change settings and backups", AdminGroup),
        new(ActivitySee, "See activity log", AdminGroup),
        new(LabelsSettings, "Change label settings", AdminGroup),
        new(StockDamage, "Record damage", StockGroup),
        new(StockApprove, "Approve stock changes", StockGroup),
        new(StockRacks, "Set where items are kept (racks)", StockGroup),
        new(SalesOtherPlace, "Sell from another place (e.g. the godown)", StockGroup),
        new(StockTransferSend, "Send stock transfers", StockGroup),
        new(StockTransferReceive, "Receive stock transfers", StockGroup),
        new(StockTransferWriteOff, "Write off pieces missing on a stock transfer", StockGroup),
        new(RatesPurchaseSee, "See purchase rates", RatesGroup),
        new(RatesSaleSee, "See sale rates", RatesGroup),
    }.AsReadOnly();

    /// <summary>The plain description of a switch (its key if unknown, e.g. one removed in a later version).</summary>
    public static string Describe(string key) => All.FirstOrDefault(p => p.Key == key)?.Label ?? key;
}
