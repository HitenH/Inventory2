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
            });
            builder.Services.AddScoped<IUserRepository, UserRepositoryEF>();
            builder.Services.AddScoped<ICustomerRepository, CustomerRepositoryEF>();
            builder.Services.AddScoped<ISupplierRepository, SupplierRepositoryEF>();
            builder.Services.AddScoped<ProtectedSessionStorage>();
            builder.Services.AddScoped<INumberService, NumberService>();
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