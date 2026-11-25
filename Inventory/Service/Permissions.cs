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

    public const string AdminUsers = "admin.users";
    public const string AdminImport = "admin.import";
    public const string AdminSettings = "admin.settings";
    /// <summary>Batch 1c-4: the "See activity log" button and (1c-5) the log screen. Rates in the log follow the rate switches.</summary>
    public const string ActivitySee = "activity.see";

    public const string RatesPurchaseSee = "rates.purchase.see";
    public const string RatesSaleSee = "rates.sale.see";

    public const string ScreensGroup = "Screens";
    public const string AdminGroup = "Admin";
    public const string RatesGroup = "Rates";

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
        ["reports"] = ScreenReports,
        ["settings"] = AdminSettings,
        ["users"] = AdminUsers, ["roles"] = AdminUsers,
        ["activity"] = ActivitySee,
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
        new(ScreenReports, "Open the Reports screen", ScreensGroup),
        new(AdminUsers, "Manage users and roles", AdminGroup),
        new(AdminImport, "Import data", AdminGroup),
        new(AdminSettings, "Change settings and backups", AdminGroup),
        new(ActivitySee, "See activity log", AdminGroup),
        new(RatesPurchaseSee, "See purchase rates", RatesGroup),
        new(RatesSaleSee, "See sale rates", RatesGroup),
    }.AsReadOnly();

    /// <summary>The plain description of a switch (its key if unknown, e.g. one removed in a later version).</summary>
    public static string Describe(string key) => All.FirstOrDefault(p => p.Key == key)?.Label ?? key;
}
