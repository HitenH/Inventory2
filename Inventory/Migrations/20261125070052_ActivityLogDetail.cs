using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class ActivityLogDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLineDetail",
                table: "ActivityLog",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Pieces",
                table: "ActivityLog",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLineDetail",
                table: "ActivityLog");

            migrationBuilder.DropColumn(
                name: "Pieces",
                table: "ActivityLog");
        }
    }
}
