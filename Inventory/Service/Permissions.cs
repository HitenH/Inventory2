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

    public const string RatesPurchaseSee = "rates.purchase.see";
    public const string RatesSaleSee = "rates.sale.see";

    public const string ScreensGroup = "Screens";
    public const string AdminGroup = "Admin";
    public const string RatesGroup = "Rates";

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
        new(RatesPurchaseSee, "See purchase rates", RatesGroup),
        new(RatesSaleSee, "See sale rates", RatesGroup),
    }.AsReadOnly();
}
