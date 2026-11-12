using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <inheritdoc />
    /// <summary>Batch 7: sales orders store their discount (it used to be worked out backwards from the total).</summary>
    public partial class StoreSalesOrderDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "SalesOrders",
                type: "TEXT",
                nullable: true);

            // Store the discount each existing order shows on screen today. Before batch 7 the screen worked it out
            // as (sum of lines' amount after discount) - (stored total) when the stored total was above 0, else 0.
            migrationBuilder.Sql(@"
                UPDATE ""SalesOrders""
                SET ""Discount"" = CASE
                    WHEN CAST(coalesce(""TotalAmountProduct"", '0') AS REAL) > 0 THEN
                        round(coalesce((SELECT sum(CAST(l.""AmountAfterDiscount"" AS REAL)) FROM ""SalesOrderVariants"" l
                                        WHERE l.""SalesOrderEntityId"" = ""SalesOrders"".""Id""), 0)
                              - CAST(""TotalAmountProduct"" AS REAL), 2)
                    ELSE 0
                END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discount",
                table: "SalesOrders");
        }
    }
}
