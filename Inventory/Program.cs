using Inventory;
using Inventory.Authentication;
using Inventory.Domain;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Service;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
//builder.Services.AddDbContext<AppDbContext>(option =>
//{
//    option.UseSqlServer(builder.Configuration.GetSection("ConnectionString").Value);
//});



builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<PasswordService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// Database and backup locations come from appsettings.json (DataFolder / BackupFolder).
// Installed app default: %ProgramData%\InventoryApp. Development: the build folder (appsettings.Development.json).
var dataPaths = DataPaths.FromConfiguration(builder.Configuration);
var dbPath = dataPaths.DatabasePath;
builder.Services.AddSingleton(dataPaths);
// Batch 6: a factory instead of one shared context per browser tab. Every repository method creates a
// short-lived context, does its work and disposes it (Microsoft's guidance for Blazor Server). This removes
// "a second operation was started on this context" errors and stale data between screens.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<IUserRepository, UserRepositoryEF>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepositoryEF>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepositoryEF>();
builder.Services.AddScoped<IProductRepository, ProductRepositoryEF>();
builder.Services.AddScoped<IVariantRepository, VariantRepositoryEF>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepositoryEF>();
builder.Services.AddScoped<IMobileRepository, MobileRepositoryEF>();
builder.Services.AddScoped<IImageRepository, ImageRepositoryEF>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepositoryEF>();
builder.Services.AddScoped<IPurchaseRepository, PurchaseRepositoryEF>();
builder.Services.AddScoped<IPurchaseVariantRepository, PurchaseVariantRepositoryEF>();
builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepositoryEF>();
builder.Services.AddScoped<ISalesOrderVariantRepository, SalesOrderVariantRepositoryEF>();
builder.Services.AddScoped<ISaleRepository, SaleRepositoryEF>();
builder.Services.AddTransient<ProtectedSessionStorage>();
builder.Services.AddTransient<IMobileService, MobileService>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupScheduler>();
builder.Services.AddAutoMapper(typeof(AppMappingProfile));
builder.Services.AddBlazorBootstrap();
builder.Services.AddMudServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    startupLogger.LogInformation("Database: {Database}  Backups: {Backups}", dataPaths.DatabasePath, dataPaths.BackupFolder);

    // One-time move of Inventory.db from the program folder to the data folder (copy only, never deletes).
    dataPaths.MoveLegacyDatabaseIfNeeded(startupLogger);

    using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        // The update runs in a transaction, so a failure leaves the data exactly as it was.
        startupLogger.LogCritical(ex, "Database update failed. The data has NOT been changed.");
        try
        {
            foreach (var duplicate in DbErrors.FindDuplicateIds(dbPath))
            {
                startupLogger.LogCritical("Must be fixed before this version can start: {Duplicate}", duplicate);
            }
        }
        catch (Exception checkEx)
        {
            startupLogger.LogError(checkEx, "Could not check for duplicate IDs");
        }

        throw;
    }

    await UserBootstrap.RunAsync(
        db,
        scope.ServiceProvider.GetRequiredService<PasswordService>(),
        startupLogger,
        dataPaths.DataFolder);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    OpenBrowser("http://localhost:5000");
}


app.UseAuthentication();
app.UseAntiforgery();

app.UseStaticFiles();

app.MapStaticAssets();
// Login is kept in browser session storage, which only the live Blazor connection can read.
// So the login check happens inside the app (AuthorizeRouteView in Routes.razor redirects to /login),
// not on the first HTTP request. AllowAnonymous here stops ASP.NET from trying (and failing) to
// challenge the plain page request, which caused "No DefaultChallengeScheme found" errors on refresh.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.Run();

static void OpenBrowser(string url)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to open browser: {ex.Message}");
    }
}