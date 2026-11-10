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

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Inventory.db");
builder.Services.AddDbContext<AppDbContext>(options =>
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
builder.Services.AddSingleton(_ => new BackupService(
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups"),
    dbPath));
builder.Services.AddAutoMapper(typeof(AppMappingProfile));
builder.Services.AddBlazorBootstrap();
builder.Services.AddMudServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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