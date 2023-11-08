using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddSomeChanges3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders",
                column: "CustomerEntityId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Customers_CustomerEntityId",
                table: "SalesOrders",
                column: "CustomerEntityId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
