using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddSomeChanges7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesSummaryEntity_Sales_SalesEntityId",
                table: "SalesSummaryEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SalesSummaryEntity",
                table: "SalesSummaryEntity");

            migrationBuilder.RenameTable(
                name: "SalesSummaryEntity",
                newName: "SalesSummary");

            migrationBuilder.RenameIndex(
                name: "IX_SalesSummaryEntity_SalesEntityId",
                table: "SalesSummary",
                newName: "IX_SalesSummary_SalesEntityId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SalesSummary",
                table: "SalesSummary",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesSummary_Sales_SalesEntityId",
                table: "SalesSummary",
                column: "SalesEntityId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesSummary_Sales_SalesEntityId",
                table: "SalesSummary");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SalesSummary",
                table: "SalesSummary");

            migrationBuilder.RenameTable(
                name: "SalesSummary",
                newName: "SalesSummaryEntity");

            migrationBuilder.RenameIndex(
                name: "IX_SalesSummary_SalesEntityId",
                table: "SalesSummaryEntity",
                newName: "IX_SalesSummaryEntity_SalesEntityId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SalesSummaryEntity",
                table: "SalesSummaryEntity",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesSummaryEntity_Sales_SalesEntityId",
                table: "SalesSummaryEntity",
                column: "SalesEntityId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
