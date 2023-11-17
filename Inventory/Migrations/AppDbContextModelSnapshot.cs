﻿// <auto-generated />
using System;
using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Inventory.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Inventory.Domain.Entities.CategoryEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("CategoryId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Categories");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.CustomerEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Address")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Area")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContactPerson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("CustomerId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Remarks")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.Image", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<byte[]>("ImageData")
                        .HasColumnType("varbinary(max)");

                    b.Property<string>("ImageTitle")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("VariantEntityId")
                        .IsRequired()
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("VariantEntityId")
                        .IsUnique();

                    b.ToTable("Images");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.Mobile", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<Guid?>("CustomerEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Phone")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("SupplierEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("CustomerEntityId");

                    b.HasIndex("SupplierEntityId");

                    b.ToTable("Mobiles");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.ProductEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("CategoryId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ProductId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal?>("Rate")
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("Id");

                    b.HasIndex("CategoryId");

                    b.ToTable("Products");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.PurchaseEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<decimal?>("Discount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Remarks")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("SupplierEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("TotalAmountProduct")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("VoucherId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("SupplierEntityId");

                    b.ToTable("Purchases");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.PurchaseOrderEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("Date")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DueDate")
                        .HasColumnType("datetime2");

                    b.Property<int?>("OrderStatus")
                        .HasColumnType("int");

                    b.Property<string>("ProductId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ProductName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal?>("ProductRate")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int?>("Quantity")
                        .HasColumnType("int");

                    b.Property<string>("Remarks")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SupplierId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VariantId")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("PurchaseOrders");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.PurchaseVariant", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("Amount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal?>("AmountAfterDiscount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int?>("Discount")
                        .HasColumnType("int");

                    b.Property<Guid>("ProductEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("ProductRate")
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("PurchaseEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int?>("Quantity")
                        .HasColumnType("int");

                    b.Property<int>("SerialNumber")
                        .HasColumnType("int");

                    b.Property<Guid>("VariantEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ProductEntityId");

                    b.HasIndex("PurchaseEntityId");

                    b.HasIndex("VariantEntityId");

                    b.ToTable("PurchaseVariant");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CustomerEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<decimal>("Discoint")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Remarks")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("TotalAmountProduct")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("TotalQuantity")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("VoucherId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("CustomerEntityId");

                    b.ToTable("Sales");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesOrderEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CustomerEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("DueDate")
                        .HasColumnType("datetime2");

                    b.Property<int>("OrderStatus")
                        .HasColumnType("int");

                    b.Property<decimal?>("TotalAmountProduct")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("VoucherId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("CustomerEntityId");

                    b.ToTable("SalesOrders");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesOrderVariantEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("Amount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal?>("AmountAfterDiscount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int?>("Discount")
                        .HasColumnType("int");

                    b.Property<Guid>("ProductEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("ProductRate")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int?>("Quantity")
                        .HasColumnType("int");

                    b.Property<string>("Remarks")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("SalesOrderEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("SerialNumber")
                        .HasColumnType("int");

                    b.Property<Guid>("VariantEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ProductEntityId");

                    b.HasIndex("SalesOrderEntityId");

                    b.HasIndex("VariantEntityId");

                    b.ToTable("SalesOrderVariants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesSummaryEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("AmountAfterDiscount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Discount")
                        .HasColumnType("int");

                    b.Property<Guid>("ProductEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ProductId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ProductName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("ProductRate")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Quantity")
                        .HasColumnType("int");

                    b.Property<Guid>("SalesEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("SerialNumber")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("SalesEntityId");

                    b.ToTable("SalesSummary");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesVariantEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ProductEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int?>("Quantity")
                        .HasColumnType("int");

                    b.Property<Guid>("SalesEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("VariantEntityId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ProductEntityId");

                    b.HasIndex("SalesEntityId");

                    b.HasIndex("VariantEntityId");

                    b.ToTable("SalesVariants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SupplierEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Address")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Area")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContactPerson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Remarks")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SupplierId")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Suppliers");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.UserEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Users");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Name = "Hiten",
                            Password = "J031289",
                            Role = "User"
                        },
                        new
                        {
                            Id = 2,
                            Name = "Admin",
                            Password = "Admin@",
                            Role = "Admin"
                        });
                });

            modelBuilder.Entity("Inventory.Domain.Entities.VariantEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("ProductEntityId")
                        .IsRequired()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("VariantId")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("ProductEntityId");

                    b.ToTable("Variants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.Image", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.VariantEntity", "Variant")
                        .WithOne("Image")
                        .HasForeignKey("Inventory.Domain.Entities.Image", "VariantEntityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Variant");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.Mobile", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.CustomerEntity", "Customer")
                        .WithMany("Mobiles")
                        .HasForeignKey("CustomerEntityId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Inventory.Domain.Entities.SupplierEntity", "Supplier")
                        .WithMany("Mobiles")
                        .HasForeignKey("SupplierEntityId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Customer");

                    b.Navigation("Supplier");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.ProductEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.CategoryEntity", "Category")
                        .WithMany("Products")
                        .HasForeignKey("CategoryId");

                    b.Navigation("Category");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.PurchaseEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.SupplierEntity", "Supplier")
                        .WithMany("Purchases")
                        .HasForeignKey("SupplierEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Supplier");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.PurchaseVariant", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.ProductEntity", "Product")
                        .WithMany("PurchaseVariants")
                        .HasForeignKey("ProductEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("Inventory.Domain.Entities.PurchaseEntity", "Purchase")
                        .WithMany("PurchaseVariants")
                        .HasForeignKey("PurchaseEntityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Inventory.Domain.Entities.VariantEntity", "ProductVariant")
                        .WithMany("PurchaseVariants")
                        .HasForeignKey("VariantEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Product");

                    b.Navigation("ProductVariant");

                    b.Navigation("Purchase");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.CustomerEntity", "Customer")
                        .WithMany("Sales")
                        .HasForeignKey("CustomerEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesOrderEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.CustomerEntity", "Customer")
                        .WithMany("SalesOrders")
                        .HasForeignKey("CustomerEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesOrderVariantEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.ProductEntity", "Product")
                        .WithMany("SalesOrderVariants")
                        .HasForeignKey("ProductEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("Inventory.Domain.Entities.SalesOrderEntity", "SalesOrder")
                        .WithMany("SalesOrderVariants")
                        .HasForeignKey("SalesOrderEntityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Inventory.Domain.Entities.VariantEntity", "ProductVariant")
                        .WithMany("SalesOrderVariants")
                        .HasForeignKey("VariantEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Product");

                    b.Navigation("ProductVariant");

                    b.Navigation("SalesOrder");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesSummaryEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.SalesEntity", "Sale")
                        .WithMany("SalesSummaries")
                        .HasForeignKey("SalesEntityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Sale");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesVariantEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.ProductEntity", "Product")
                        .WithMany("SalesVariants")
                        .HasForeignKey("ProductEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("Inventory.Domain.Entities.SalesEntity", "Sale")
                        .WithMany("SalesVariants")
                        .HasForeignKey("SalesEntityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Inventory.Domain.Entities.VariantEntity", "ProductVariant")
                        .WithMany("SalesVariants")
                        .HasForeignKey("VariantEntityId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Product");

                    b.Navigation("ProductVariant");

                    b.Navigation("Sale");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.VariantEntity", b =>
                {
                    b.HasOne("Inventory.Domain.Entities.ProductEntity", "Product")
                        .WithMany("Variants")
                        .HasForeignKey("ProductEntityId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Product");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.CategoryEntity", b =>
                {
                    b.Navigation("Products");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.CustomerEntity", b =>
                {
                    b.Navigation("Mobiles");

                    b.Navigation("Sales");

                    b.Navigation("SalesOrders");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.ProductEntity", b =>
                {
                    b.Navigation("PurchaseVariants");

                    b.Navigation("SalesOrderVariants");

                    b.Navigation("SalesVariants");

                    b.Navigation("Variants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.PurchaseEntity", b =>
                {
                    b.Navigation("PurchaseVariants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesEntity", b =>
                {
                    b.Navigation("SalesSummaries");

                    b.Navigation("SalesVariants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SalesOrderEntity", b =>
                {
                    b.Navigation("SalesOrderVariants");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.SupplierEntity", b =>
                {
                    b.Navigation("Mobiles");

                    b.Navigation("Purchases");
                });

            modelBuilder.Entity("Inventory.Domain.Entities.VariantEntity", b =>
                {
                    b.Navigation("Image");

                    b.Navigation("PurchaseVariants");

                    b.Navigation("SalesOrderVariants");

                    b.Navigation("SalesVariants");
                });
#pragma warning restore 612, 618
        }
    }
}
