using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <summary>
    /// Batch 5 (owner's request, 12 Nov 2026), from now on:
    /// 1. IDs are unique ignoring upper/lower case ("abc1" is refused when "ABC1" exists).
    ///    The four unique indexes from UniqueBusinessIds are recreated with COLLATE NOCASE, keeping their names,
    ///    so EF's model (plain unique indexes) still matches. NOTE: if a future migration ever REBUILDS one of these
    ///    tables (Products, Customers, Suppliers, Variants), EF would recreate the index without NOCASE –
    ///    re-run the CREATE INDEX statements below in that migration.
    /// 2. No new blank records: a product, variant, customer or supplier must have an ID and a name, and a variant
    ///    must belong to a product. Sale, purchase and sales-order lines must point to a real product and one of
    ///    its variants. Enforced with triggers, which only check new and changed rows: old blank rows stay as they are.
    ///    Trigger messages start with "RULE:" so the app can show them as plain text (Service/DbErrors.cs).
    /// </summary>
    public partial class CaseInsensitiveIdsNoBlanks : Migration
    {
        private static readonly string[] CaseInsensitiveIndexes =
        {
            @"CREATE UNIQUE INDEX ""IX_Products_ProductId"" ON ""Products"" (""ProductId"" COLLATE NOCASE);",
            @"CREATE UNIQUE INDEX ""IX_Customers_CustomerId"" ON ""Customers"" (""CustomerId"" COLLATE NOCASE);",
            @"CREATE UNIQUE INDEX ""IX_Suppliers_SupplierId"" ON ""Suppliers"" (""SupplierId"" COLLATE NOCASE);",
            @"CREATE UNIQUE INDEX ""IX_Variants_ProductEntityId_VariantId"" ON ""Variants"" (""ProductEntityId"", ""VariantId"" COLLATE NOCASE);",
        };

        private static readonly string[] IndexNames =
        {
            "IX_Products_ProductId", "IX_Customers_CustomerId", "IX_Suppliers_SupplierId", "IX_Variants_ProductEntityId_VariantId"
        };

        private static string Blank(string column) => $@"(NEW.""{column}"" IS NULL OR trim(NEW.""{column}"") = '')";

        private static IEnumerable<string> MasterTriggers()
        {
            var masters = new (string Table, string IdColumn, string Label)[]
            {
                ("Products", "ProductId", "Product"),
                ("Customers", "CustomerId", "Customer"),
                ("Suppliers", "SupplierId", "Supplier"),
            };

            foreach (var (table, id, label) in masters)
            {
                var when = $"{Blank(id)} OR {Blank("Name")}";
                var raise = $"SELECT RAISE(ABORT, 'RULE: {label} ID and name are required.');";
                yield return $@"CREATE TRIGGER ""TR_{table}_NoBlank_Insert"" BEFORE INSERT ON ""{table}"" WHEN {when} BEGIN {raise} END;";
                yield return $@"CREATE TRIGGER ""TR_{table}_NoBlank_Update"" BEFORE UPDATE OF ""{id}"", ""Name"" ON ""{table}"" WHEN {when} BEGIN {raise} END;";
            }

            var variantWhen = $@"{Blank("VariantId")} OR {Blank("Name")} OR NEW.""ProductEntityId"" IS NULL";
            var variantRaise = "SELECT RAISE(ABORT, 'RULE: Variant ID and name are required, and the variant must belong to a product.');";
            yield return $@"CREATE TRIGGER ""TR_Variants_NoBlank_Insert"" BEFORE INSERT ON ""Variants"" WHEN {variantWhen} BEGIN {variantRaise} END;";
            yield return $@"CREATE TRIGGER ""TR_Variants_NoBlank_Update"" BEFORE UPDATE OF ""VariantId"", ""Name"", ""ProductEntityId"" ON ""Variants"" WHEN {variantWhen} BEGIN {variantRaise} END;";
        }

        private static IEnumerable<string> LineTriggers()
        {
            // A line must point to a variant that belongs to the line's product, and both must have real IDs.
            const string notReal = @"NOT EXISTS (
                SELECT 1 FROM ""Variants"" v JOIN ""Products"" p ON p.""Id"" = v.""ProductEntityId""
                WHERE v.""Id"" = NEW.""VariantEntityId"" AND p.""Id"" = NEW.""ProductEntityId""
                  AND trim(coalesce(v.""VariantId"", '')) <> '' AND trim(coalesce(p.""ProductId"", '')) <> '')";
            const string raise = "SELECT RAISE(ABORT, 'RULE: Each line must be a real product and one of its variants.');";

            foreach (var table in new[] { "SalesVariants", "PurchaseVariant", "SalesOrderVariants" })
            {
                yield return $@"CREATE TRIGGER ""TR_{table}_RealItem_Insert"" BEFORE INSERT ON ""{table}"" WHEN {notReal} BEGIN {raise} END;";
                yield return $@"CREATE TRIGGER ""TR_{table}_RealItem_Update"" BEFORE UPDATE OF ""ProductEntityId"", ""VariantEntityId"" ON ""{table}"" WHEN {notReal} BEGIN {raise} END;";
            }
        }

        private static IEnumerable<string> TriggerNames()
        {
            foreach (var t in new[] { "Products", "Customers", "Suppliers", "Variants" })
            {
                yield return $"TR_{t}_NoBlank_Insert";
                yield return $"TR_{t}_NoBlank_Update";
            }
            foreach (var t in new[] { "SalesVariants", "PurchaseVariant", "SalesOrderVariants" })
            {
                yield return $"TR_{t}_RealItem_Insert";
                yield return $"TR_{t}_RealItem_Update";
            }
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var name in IndexNames)
                migrationBuilder.Sql($@"DROP INDEX IF EXISTS ""{name}"";");

            foreach (var sql in CaseInsensitiveIndexes)
                migrationBuilder.Sql(sql);

            foreach (var sql in MasterTriggers().Concat(LineTriggers()))
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in TriggerNames())
                migrationBuilder.Sql($@"DROP TRIGGER IF EXISTS ""{name}"";");

            foreach (var name in IndexNames)
                migrationBuilder.Sql($@"DROP INDEX IF EXISTS ""{name}"";");

            // Back to the case-sensitive indexes of UniqueBusinessIds.
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_Products_ProductId"" ON ""Products"" (""ProductId"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_Customers_CustomerId"" ON ""Customers"" (""CustomerId"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_Suppliers_SupplierId"" ON ""Suppliers"" (""SupplierId"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_Variants_ProductEntityId_VariantId"" ON ""Variants"" (""ProductEntityId"", ""VariantId"");");
        }
    }
}
