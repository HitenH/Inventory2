using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddSomeChanges6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesSummaries_Customers_CustomerEntityId",
                table: "SalesSummaries");

            migrationBuilder.DropTable(
                name: "SalesSummaryVariants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SalesSummaries",
                table: "SalesSummaries");

            migrationBuilder.DropIndex(
                name: "IX_SalesSummaries_CustomerEntityId",
                table: "SalesSummaries");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "SalesSummaries");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "SalesSummaries");

            migrationBuilder.RenameTable(
                name: "SalesSummaries",
                newName: "SalesSummaryEntity");

            migrationBuilder.RenameColumn(
                name: "VoucherId",
                table: "SalesSummaryEntity",
                newName: "SerialNumber");

            migrationBuilder.RenameColumn(
                name: "TotalAmountAfterDiscount",
                table: "SalesSummaryEntity",
                newName: "ProductRate");

            migrationBuilder.RenameColumn(
                name: "SalesId",
                table: "SalesSummaryEntity",
                newName: "SalesEntityId");

            migrationBuilder.RenameColumn(
                name: "CustomerEntityId",
                table: "SalesSummaryEntity",
                newName: "ProductEntityId");

            migrationBuilder.AlterColumn<int>(
                name: "Discount",
                table: "SalesSummaryEntity",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "SalesSummaryEntity",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountAfterDiscount",
                table: "SalesSummaryEntity",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "SalesSummaryEntity",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SalesSummaryEntity",
                table: "SalesSummaryEntity",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SalesSummaryEntity_SalesEntityId",
                table: "SalesSummaryEntity",
                column: "SalesEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesSummaryEntity_Sales_SalesEntityId",
                table: "SalesSummaryEntity",
                column: "SalesEntityId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesSummaryEntity_Sales_SalesEntityId",
                table: "SalesSummaryEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SalesSummaryEntity",
                table: "SalesSummaryEntity");

            migrationBuilder.DropIndex(
                name: "IX_SalesSummaryEntity_SalesEntityId",
                table: "SalesSummaryEntity");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "SalesSummaryEntity");

            migrationBuilder.DropColumn(
                name: "AmountAfterDiscount",
                table: "SalesSummaryEntity");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "SalesSummaryEntity");

            migrationBuilder.RenameTable(
                name: "SalesSummaryEntity",
                newName: "SalesSummaries");

            migrationBuilder.RenameColumn(
                name: "SerialNumber",
                table: "SalesSummaries",
                newName: "VoucherId");

            migrationBuilder.RenameColumn(
                name: "SalesEntityId",
                table: "SalesSummaries",
                newName: "SalesId");

            migrationBuilder.RenameColumn(
                name: "ProductRate",
                table: "SalesSummaries",
                newName: "TotalAmountAfterDiscount");

            migrationBuilder.RenameColumn(
                name: "ProductEntityId",
                table: "SalesSummaries",
                newName: "CustomerEntityId");

            migrationBuilder.AlterColumn<decimal>(
                name: "Discount",
                table: "SalesSummaries",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "SalesSummaries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "SalesSummaries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SalesSummaries",
                table: "SalesSummaries",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "SalesSummaryVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesSummaryEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountAfterDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProductRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesSummaryVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesSummaryVariants_Products_ProductEntityId",
                        column: x => x.ProductEntityId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalesSummaryVariants_SalesSummaries_SalesSummaryEntityId",
                        column: x => x.SalesSummaryEntityId,
                        principalTable: "SalesSummaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesSummaries_CustomerEntityId",
                table: "SalesSummaries",
                column: "CustomerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesSummaryVariants_ProductEntityId",
                table: "SalesSummaryVariants",
                column: "ProductEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesSummaryVariants_SalesSummaryEntityId",
                table: "SalesSummaryVariants",
                column: "SalesSummaryEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesSummaries_Customers_CustomerEntityId",
                table: "SalesSummaries",
                column: "CustomerEntityId",
                principalTable: "Customers",
                principalColumn: "Id");
        }
    }
}
