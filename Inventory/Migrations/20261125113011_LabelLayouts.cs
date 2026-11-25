using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class LabelLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabelLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PageWidthMm = table.Column<double>(type: "REAL", nullable: false),
                    PageHeightMm = table.Column<double>(type: "REAL", nullable: false),
                    Columns = table.Column<int>(type: "INTEGER", nullable: false),
                    Rows = table.Column<int>(type: "INTEGER", nullable: false),
                    LabelWidthMm = table.Column<double>(type: "REAL", nullable: false),
                    LabelHeightMm = table.Column<double>(type: "REAL", nullable: false),
                    GapAcrossMm = table.Column<double>(type: "REAL", nullable: false),
                    GapDownMm = table.Column<double>(type: "REAL", nullable: false),
                    TopMm = table.Column<double>(type: "REAL", nullable: false),
                    LeftMm = table.Column<double>(type: "REAL", nullable: false),
                    ContentDownMm = table.Column<double>(type: "REAL", nullable: false),
                    ContentLeftMm = table.Column<double>(type: "REAL", nullable: false),
                    QrMm = table.Column<double>(type: "REAL", nullable: false),
                    NameFontPt = table.Column<double>(type: "REAL", nullable: false),
                    VariantFontPt = table.Column<double>(type: "REAL", nullable: false),
                    IdFontPt = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelLayouts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LabelLayouts",
                columns: new[] { "Id", "ChangedAt", "ChangedBy", "Columns", "ContentDownMm", "ContentLeftMm", "CreatedAt", "CreatedBy", "GapAcrossMm", "GapDownMm", "IdFontPt", "IsActive", "LabelHeightMm", "LabelWidthMm", "LeftMm", "Name", "NameFontPt", "PageHeightMm", "PageWidthMm", "QrMm", "Rows", "TopMm", "VariantFontPt" },
                values: new object[] { 1, null, null, 5, 2.2000000000000002, 0.5, null, null, 0.0, 0.0, 7.5, true, 21.0, 40.0, 4.0, "A4 65 Label", 9.0, 297.0, 210.0, 13.0, 13, 12.0, 11.0 });

            migrationBuilder.CreateIndex(
                name: "IX_LabelLayouts_Name",
                table: "LabelLayouts",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabelLayouts");
        }
    }
}
