using Inventory.Authentication;
using Inventory.Domain;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Service;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace Inventory
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddDbContext<AppDbContext>(option =>
            {
                option.UseSqlServer(builder.Configuration.GetSection("ConnectionString").Value);
                
            }, ServiceLifetime.Transient);

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
            builder.Services.AddTransient<ProtectedSessionStorage>();
            builder.Services.AddTransient<IMobileService, MobileService>();
            builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationProvider>();
            builder.Services.AddAutoMapper(typeof(AppMappingProfile));
            builder.Services.AddBlazorBootstrap();


            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}