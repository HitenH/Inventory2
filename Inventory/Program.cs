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

var builder = WebApplication.CreateBuilder(args);


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

builder.Services.AddTransient<IUserRepository, UserRepositoryEF>();
builder.Services.AddTransient<ICustomerRepository, CustomerRepositoryEF>();
builder.Services.AddTransient<ISupplierRepository, SupplierRepositoryEF>();
builder.Services.AddTransient<IProductRepository, ProductRepositoryEF>();
builder.Services.AddTransient<IVariantRepository, VariantRepositoryEF>();
builder.Services.AddTransient<ICategoryRepository, CategoryRepositoryEF>();
builder.Services.AddTransient<IMobileRepository, MobileRepositoryEF>();
builder.Services.AddTransient<IImageRepository, ImageRepositoryEF>();
builder.Services.AddTransient<IPurchaseOrderRepository, PurchaseOrderRepositoryEF>();
builder.Services.AddTransient<IPurchaseRepository, PurchaseRepositoryEF>();
builder.Services.AddTransient<IPurchaseVariantRepository, PurchaseVariantRepositoryEF>();
builder.Services.AddTransient<ISalesOrderRepository, SalesOrderRepositoryEF>();
builder.Services.AddTransient<ISalesOrderVariantRepository, SalesOrderVariantRepositoryEF>();
builder.Services.AddTransient<ISaleRepository, SaleRepositoryEF>();
builder.Services.AddTransient<ProtectedSessionStorage>();
builder.Services.AddTransient<IMobileService, MobileService>();
builder.Services.AddAutoMapper(typeof(AppMappingProfile));
builder.Services.AddBlazorBootstrap();
builder.Services.AddMudServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAuthentication();
app.UseAntiforgery();

app.UseStaticFiles();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();