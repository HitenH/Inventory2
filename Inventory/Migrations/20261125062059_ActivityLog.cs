using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class ActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    By = table.Column<string>(type: "TEXT", nullable: true),
                    SaveId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    RecordType = table.Column<string>(type: "TEXT", nullable: true),
                    RecordId = table.Column<string>(type: "TEXT", nullable: true),
                    RecordLabel = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    RateSide = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_At",
                table: "ActivityLog",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_RecordType_RecordId",
                table: "ActivityLog",
                columns: new[] { "RecordType", "RecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLog");
        }
    }
}
