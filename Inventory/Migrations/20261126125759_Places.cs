using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Migrations
{
    /// <inheritdoc />
    public partial class Places : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromPlaceId",
                table: "StockChangeLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToPlaceId",
                table: "StockChangeLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlaceId",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PlaceId",
                table: "Purchases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Places",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Places", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Racks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Levels = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Racks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Racks_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VariantRacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VariantEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RackId = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 1, nullable: false),
                    IsMain = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantRacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantRacks_Racks_RackId",
                        column: x => x.RackId,
                        principalTable: "Racks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantRacks_Variants_VariantEntityId",
                        column: x => x.VariantEntityId,
                        principalTable: "Variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Places",
                columns: new[] { "Id", "Name", "Number" },
                values: new object[,]
                {
                    { 1, "Shop", 1 },
                    { 2, "Godown", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Places_Name",
                table: "Places",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_Number",
                table: "Places",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Racks_PlaceId_Number",
                table: "Racks",
                columns: new[] { "PlaceId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantRacks_RackId",
                table: "VariantRacks",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantRacks_VariantEntityId_RackId_Level",
                table: "VariantRacks",
                columns: new[] { "VariantEntityId", "RackId", "Level" },
                unique: true);

            // Hiten, 26 Nov 2026: all existing stock starts in the shop. Purchases and sales got the shop (place 1) through
            // the column default above; stock change lines get it on their sellable side here.
            migrationBuilder.Sql(@"UPDATE ""StockChangeLines"" SET ""FromPlaceId"" = 1 WHERE ""From"" = 'Sellable';");
            migrationBuilder.Sql(@"UPDATE ""StockChangeLines"" SET ""ToPlaceId"" = 1 WHERE ""To"" = 'Sellable';");

            // Last line of defence (like the RealItem triggers): every purchase, sale and sellable side of a stock change
            // line is in a place that exists, and a place that holds entries (or the starting place) cannot be deleted.
            const string placeRaise = "SELECT RAISE(ABORT, 'RULE: Stock must be in a real place.');";
            foreach (var table in new[] { "Purchases", "Sales" })
            {
                var noPlace = $@"NOT EXISTS (SELECT 1 FROM ""Places"" WHERE ""Id"" = NEW.""PlaceId"")";
                migrationBuilder.Sql($@"CREATE TRIGGER ""TR_{table}_RealPlace_Insert"" BEFORE INSERT ON ""{table}"" WHEN {noPlace} BEGIN {placeRaise} END;");
                migrationBuilder.Sql($@"CREATE TRIGGER ""TR_{table}_RealPlace_Update"" BEFORE UPDATE OF ""PlaceId"" ON ""{table}"" WHEN {noPlace} BEGIN {placeRaise} END;");
            }

            const string lineNoPlace = @"(NEW.""From"" = 'Sellable' AND NOT EXISTS (SELECT 1 FROM ""Places"" WHERE ""Id"" = NEW.""FromPlaceId""))
                OR (NEW.""To"" = 'Sellable' AND NOT EXISTS (SELECT 1 FROM ""Places"" WHERE ""Id"" = NEW.""ToPlaceId""))";
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_RealPlace_Insert"" BEFORE INSERT ON ""StockChangeLines"" WHEN {lineNoPlace} BEGIN {placeRaise} END;");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_RealPlace_Update"" BEFORE UPDATE OF ""From"", ""To"", ""FromPlaceId"", ""ToPlaceId"" ON ""StockChangeLines"" WHEN {lineNoPlace} BEGIN {placeRaise} END;");

            // Stage 2 refused a line that does not move (from = to). A move between places is Sellable → Sellable in two
            // different places (batch 3-5), so the rule now looks at the places too.
            const string moveRaise = "SELECT RAISE(ABORT, 'RULE: A stock change line must move more than zero pieces from one place to another.');";
            const string badMove = @"NEW.""Quantity"" <= 0 OR (NEW.""From"" = NEW.""To""
                AND (NEW.""From"" <> 'Sellable' OR coalesce(NEW.""FromPlaceId"", -1) = coalesce(NEW.""ToPlaceId"", -1)))";
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS ""TR_StockChangeLines_Move_Insert"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS ""TR_StockChangeLines_Move_Update"";");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_Move_Insert"" BEFORE INSERT ON ""StockChangeLines"" WHEN {badMove} BEGIN {moveRaise} END;");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_Move_Update"" BEFORE UPDATE OF ""Quantity"", ""From"", ""To"", ""FromPlaceId"", ""ToPlaceId"" ON ""StockChangeLines"" WHEN {badMove} BEGIN {moveRaise} END;");

            const string placeUsed = @"OLD.""Id"" = 1
                OR EXISTS (SELECT 1 FROM ""Purchases"" WHERE ""PlaceId"" = OLD.""Id"")
                OR EXISTS (SELECT 1 FROM ""Sales"" WHERE ""PlaceId"" = OLD.""Id"")
                OR EXISTS (SELECT 1 FROM ""StockChangeLines"" WHERE ""FromPlaceId"" = OLD.""Id"" OR ""ToPlaceId"" = OLD.""Id"")";
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_Places_InUse_Delete"" BEFORE DELETE ON ""Places"" WHEN {placeUsed} BEGIN SELECT RAISE(ABORT, 'RULE: A place that holds stock entries cannot be deleted.'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var trigger in new[] { "Purchases_RealPlace_Insert", "Purchases_RealPlace_Update", "Sales_RealPlace_Insert", "Sales_RealPlace_Update",
                         "StockChangeLines_RealPlace_Insert", "StockChangeLines_RealPlace_Update", "Places_InUse_Delete" })
            {
                migrationBuilder.Sql($@"DROP TRIGGER IF EXISTS ""TR_{trigger}"";");
            }

            // Back to stage 2's move rule (from ≠ to).
            const string oldMove = @"NEW.""Quantity"" <= 0 OR NEW.""From"" = NEW.""To""";
            const string oldRaise = "SELECT RAISE(ABORT, 'RULE: A stock change line must move more than zero pieces from one place to another.');";
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS ""TR_StockChangeLines_Move_Insert"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS ""TR_StockChangeLines_Move_Update"";");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_Move_Insert"" BEFORE INSERT ON ""StockChangeLines"" WHEN {oldMove} BEGIN {oldRaise} END;");
            migrationBuilder.Sql($@"CREATE TRIGGER ""TR_StockChangeLines_Move_Update"" BEFORE UPDATE OF ""Quantity"", ""From"", ""To"" ON ""StockChangeLines"" WHEN {oldMove} BEGIN {oldRaise} END;");

            migrationBuilder.DropTable(
                name: "VariantRacks");

            migrationBuilder.DropTable(
                name: "Racks");

            migrationBuilder.DropTable(
                name: "Places");

            migrationBuilder.DropColumn(
                name: "FromPlaceId",
                table: "StockChangeLines");

            migrationBuilder.DropColumn(
                name: "ToPlaceId",
                table: "StockChangeLines");

            migrationBuilder.DropColumn(
                name: "PlaceId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PlaceId",
                table: "Purchases");
        }
    }
}
