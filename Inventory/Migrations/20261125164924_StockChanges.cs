using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class StockChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesVariants_VariantEntityId",
                table: "SalesVariants");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseVariant_VariantEntityId",
                table: "PurchaseVariant");

            migrationBuilder.CreateTable(
                name: "StockChangeReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockChangeReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    VoucherId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CustomerEntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupplierEntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PurchaseEntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceStockChangeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockChanges_Customers_CustomerEntityId",
                        column: x => x.CustomerEntityId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockChanges_Suppliers_SupplierEntityId",
                        column: x => x.SupplierEntityId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockChangeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StockChangeEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SerialNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VariantEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    From = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    To = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    PurchaseVariantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceLineId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReturnSupplierEntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReadyToSend = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockChangeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockChangeLines_Products_ProductEntityId",
                        column: x => x.ProductEntityId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockChangeLines_StockChanges_StockChangeEntityId",
                        column: x => x.StockChangeEntityId,
                        principalTable: "StockChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockChangeLines_Variants_VariantEntityId",
                        column: x => x.VariantEntityId,
                        principalTable: "Variants",
                        principalColumn: "Id");
                });

            // Last line of defence, like the purchase and sale lines (CaseInsensitiveIdsNoBlanks): a stock change line
            // must be a real product and one of its variants, more than zero pieces, and a real move (from ≠ to).
            const string notReal = @"NOT EXISTS (
                SELECT 1 FROM ""Variants"" v JOIN ""Products"" p ON p.""Id"" = v.""ProductEntityId""
                WHERE v.""Id"" = NEW.""VariantEntityId"" AND p.""Id"" = NEW.""ProductEntityId""
                  AND trim(coalesce(v.""VariantId"", '')) <> '' AND trim(coalesce(p.""ProductId"", '')) <> '')";
            const string realRaise = "SELECT RAISE(ABORT, 'RULE: Each line must be a real product and one of its variants.');";
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_RealItem_Insert"" BEFORE INSERT ON ""StockChangeLines"" WHEN {notReal} BEGIN {realRaise} END;");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_RealItem_Update"" BEFORE UPDATE OF ""ProductEntityId"", ""VariantEntityId"" ON ""StockChangeLines"" WHEN {notReal} BEGIN {realRaise} END;");
            const string badMove = @"NEW.""Quantity"" <= 0 OR NEW.""From"" = NEW.""To""";
            const string moveRaise = "SELECT RAISE(ABORT, 'RULE: A stock change line must move more than zero pieces from one place to another.');";
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_Move_Insert"" BEFORE INSERT ON ""StockChangeLines"" WHEN {badMove} BEGIN {moveRaise} END;");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_Move_Update"" BEFORE UPDATE OF ""Quantity"", ""From"", ""To"" ON ""StockChangeLines"" WHEN {badMove} BEGIN {moveRaise} END;");

            migrationBuilder.InsertData(
                table: "StockChangeReasons",
                columns: new[] { "Id", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, true, "Broken in transit", 1 },
                    { 2, true, "Manufacturing defect", 2 },
                    { 3, true, "Damaged in shop", 3 },
                    { 4, true, "Wrong item received", 4 },
                    { 5, true, "Count difference", 5 },
                    { 6, true, "Other", 6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesVariants_VariantEntityId_Quantity",
                table: "SalesVariants",
                columns: new[] { "VariantEntityId", "Quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseVariant_VariantEntityId_Quantity",
                table: "PurchaseVariant",
                columns: new[] { "VariantEntityId", "Quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_StockChangeLines_ProductEntityId",
                table: "StockChangeLines",
                column: "ProductEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChangeLines_PurchaseVariantId",
                table: "StockChangeLines",
                column: "PurchaseVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChangeLines_SourceLineId",
                table: "StockChangeLines",
                column: "SourceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChangeLines_StockChangeEntityId",
                table: "StockChangeLines",
                column: "StockChangeEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChangeLines_VariantEntityId",
                table: "StockChangeLines",
                column: "VariantEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChangeReasons_Name",
                table: "StockChangeReasons",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_CustomerEntityId",
                table: "StockChanges",
                column: "CustomerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_Kind_Date",
                table: "StockChanges",
                columns: new[] { "Kind", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_PurchaseEntityId",
                table: "StockChanges",
                column: "PurchaseEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_StockChanges_SupplierEntityId",
                table: "StockChanges",
                column: "SupplierEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var trigger in new[] { "RealItem_Insert", "RealItem_Update", "Move_Insert", "Move_Update" })
            {
                migrationBuilder.Sql($@"DROP TRIGGER IF EXISTS ""TR_StockChangeLines_{trigger}"";");
            }

            migrationBuilder.DropTable(
                name: "StockChangeLines");

            migrationBuilder.DropTable(
                name: "StockChangeReasons");

            migrationBuilder.DropTable(
                name: "StockChanges");

            migrationBuilder.DropIndex(
                name: "IX_SalesVariants_VariantEntityId_Quantity",
                table: "SalesVariants");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseVariant_VariantEntityId_Quantity",
                table: "PurchaseVariant");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVariants_VariantEntityId",
                table: "SalesVariants",
                column: "VariantEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseVariant_VariantEntityId",
                table: "PurchaseVariant",
                column: "VariantEntityId");
        }
    }
}
