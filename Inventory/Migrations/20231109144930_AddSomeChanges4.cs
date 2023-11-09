using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddSomeChanges4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SalesSummaryId",
                table: "SalesSummaryVariants",
                newName: "SalesSummaryEntityId");

            migrationBuilder.AddColumn<Guid>(
                name: "SalesId",
                table: "SalesSummaryVariants",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SalesSummaryVariants_SalesSummaryEntityId",
                table: "SalesSummaryVariants",
                column: "SalesSummaryEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesSummaryVariants_SalesSummaries_SalesSummaryEntityId",
                table: "SalesSummaryVariants",
                column: "SalesSummaryEntityId",
                principalTable: "SalesSummaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesSummaryVariants_SalesSummaries_SalesSummaryEntityId",
                table: "SalesSummaryVariants");

            migrationBuilder.DropIndex(
                name: "IX_SalesSummaryVariants_SalesSummaryEntityId",
                table: "SalesSummaryVariants");

            migrationBuilder.DropColumn(
                name: "SalesId",
                table: "SalesSummaryVariants");

            migrationBuilder.RenameColumn(
                name: "SalesSummaryEntityId",
                table: "SalesSummaryVariants",
                newName: "SalesSummaryId");
        }
    }
}
