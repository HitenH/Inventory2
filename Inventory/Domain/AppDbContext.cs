using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;
using System;


namespace Inventory.Domain
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options):base(options) { }

        public DbSet<UserEntity> Users { get; set; }
        //public DbSet<CustomerEntity> Customers { get; set; }
        //public DbSet<SupplierEntity> Suppliers { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<VariantEntity> Variants { get; set; }
        public DbSet<CategoryEntity> Categories { get; set; }
       // public DbSet<Mobile> Mobiles { get; set; }
        public DbSet<Image> Images { get; set; }
        //public DbSet<PurchaseOrderEntity> PurchaseOrders { get; set; }
        //public DbSet<PurchaseEntity> Purchases { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>().HasData(new List<UserEntity>()
            {
                new UserEntity(){ Id = 1, Name = "Hiten", Password = "Hiten", Role = "User"},
                new UserEntity(){ Id = 2, Name = "Admin", Password = "admin", Role = "Admin"}
            });

            //modelBuilder.Entity<PurchaseOrderEntity>()
            //            .Property(p => p.Date)
            //            .HasConversion<DateOnlyConverter>();

            //modelBuilder.Entity<PurchaseOrderEntity>()
            //            .Property(p => p.DueDate)
            //            .HasConversion<DateOnlyConverter>();

            //modelBuilder.Entity<PurchaseEntity>()
            //           .Property(p => p.Date)
            //           .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<CategoryEntity>()
                        .HasOne(p => p.Product)
                        .WithOne(p => p.Category)
                        .HasForeignKey<ProductEntity>(k => k.CategoryEntityId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductEntity>()
                        .HasMany(p => p.Variants)
                        .WithOne(v => v.Product)
                        .HasForeignKey(k => k.ProductEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VariantEntity>()
                        .HasOne(v => v.Image)
                        .WithOne(i => i.Variant)
                        .HasForeignKey<Image>(k => k.VariantEntityId)
                        .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
