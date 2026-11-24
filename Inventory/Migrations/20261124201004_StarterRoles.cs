using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <summary>
    /// Batch 1b-5: three ready-made roles for today's screens, added once per database (an upgrade runs only once,
    /// so a starter role the owner deletes does not come back). A role whose name is already taken is skipped,
    /// and switches are only added to a role that has none. Admin is created at start-up by RoleBootstrap.
    /// </summary>
    public partial class StarterRoles : Migration
    {
        private static readonly (string Name, string[] Keys)[] Starters =
        {
            ("Purchase", new[] { "screen.dashboard", "screen.suppliers", "screen.products", "screen.purchase-orders", "screen.purchases", "rates.purchase.see" }),
            ("Sales", new[] { "screen.dashboard", "screen.customers", "screen.products", "screen.sales-orders", "screen.sales", "rates.sale.see" }),
            ("Reports", new[] { "screen.dashboard", "screen.reports" }),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, keys) in Starters)
            {
                migrationBuilder.Sql($@"INSERT INTO ""Roles"" (""Name"", ""IsAdmin"")
                    SELECT '{name}', 0 WHERE NOT EXISTS (SELECT 1 FROM ""Roles"" WHERE ""Name"" = '{name}' COLLATE NOCASE);");

                var values = string.Join(" UNION ALL ", keys.Select(k => $"SELECT '{k}' AS k"));
                migrationBuilder.Sql($@"INSERT INTO ""RolePermissions"" (""RoleId"", ""Permission"")
                    SELECT r.""Id"", p.k FROM ""Roles"" r, ({values}) p
                    WHERE r.""Name"" = '{name}' COLLATE NOCASE AND r.""IsAdmin"" = 0
                      AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" x WHERE x.""RoleId"" = r.""Id"");");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Roles may be in use by then; going back leaves them in place (the older build ignores them).
        }
    }
}
