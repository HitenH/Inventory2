using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class LabelSheetUse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabelSheetUse",
                columns: table => new
                {
                    LabelLayoutId = table.Column<int>(type: "INTEGER", nullable: false),
                    Used = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelSheetUse", x => x.LabelLayoutId);
                    table.ForeignKey(
                        name: "FK_LabelSheetUse_LabelLayouts_LabelLayoutId",
                        column: x => x.LabelLayoutId,
                        principalTable: "LabelLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabelSheetUse");
        }
    }
}
