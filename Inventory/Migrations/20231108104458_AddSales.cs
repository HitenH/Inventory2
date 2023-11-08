using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders");

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    CustomerEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerEntityId",
                        column: x => x.CustomerEntityId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SalesVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    SalesEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesVariants_Products_ProductEntityId",
                        column: x => x.ProductEntityId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalesVariants_Sales_SalesEntityId",
                        column: x => x.SalesEntityId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesVariants_Variants_VariantEntityId",
                        column: x => x.VariantEntityId,
                        principalTable: "Variants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerEntityId",
                table: "Sales",
                column: "CustomerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVariants_ProductEntityId",
                table: "SalesVariants",
                column: "ProductEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVariants_SalesEntityId",
                table: "SalesVariants",
                column: "SalesEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVariants_VariantEntityId",
                table: "SalesVariants",
                column: "VariantEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders",
                column: "CustomerEntityId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders");

            migrationBuilder.DropTable(
                name: "SalesVariants");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders",
                column: "CustomerEntityId",
                principalTable: "Customers",
                principalColumn: "Id");
        }
    }
}
