using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class UniqueBusinessIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Variants_ProductEntityId",
                table: "Variants");

            migrationBuilder.CreateIndex(
                name: "IX_Variants_ProductEntityId_VariantId",
                table: "Variants",
                columns: new[] { "ProductEntityId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierId",
                table: "Suppliers",
                column: "SupplierId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductId",
                table: "Products",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerId",
                table: "Customers",
                column: "CustomerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Variants_ProductEntityId_VariantId",
                table: "Variants");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_SupplierId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Variants_ProductEntityId",
                table: "Variants",
                column: "ProductEntityId");
        }
    }
}
