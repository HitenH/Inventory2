using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>Turns database errors into messages an operator can act on.</summary>
public static class DbErrors
{
    /// <summary>True when the database refused a save because an ID that must be unique already exists.</summary>
    public static bool IsUniqueViolation(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException { SqliteErrorCode: 19 } sqlite
                && sqlite.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Lists IDs that appear more than once. Used when a database update fails at startup, so the log says
    /// exactly which records must be fixed before the "unique ID" rules (batch 5) can be applied.
    /// </summary>
    public static List<string> FindDuplicateIds(string databasePath)
    {
        var checks = new (string What, string Sql)[]
        {
            ("Product ID", "SELECT ProductId, COUNT(*) FROM Products WHERE ProductId IS NOT NULL GROUP BY ProductId HAVING COUNT(*) > 1"),
            ("Customer ID", "SELECT CustomerId, COUNT(*) FROM Customers WHERE CustomerId IS NOT NULL GROUP BY CustomerId HAVING COUNT(*) > 1"),
            ("Supplier ID", "SELECT SupplierId, COUNT(*) FROM Suppliers WHERE SupplierId IS NOT NULL GROUP BY SupplierId HAVING COUNT(*) > 1"),
            ("Variant ID", "SELECT p.ProductId || ' / ' || v.VariantId, COUNT(*) FROM Variants v LEFT JOIN Products p ON p.Id = v.ProductEntityId " +
                           "WHERE v.ProductEntityId IS NOT NULL AND v.VariantId IS NOT NULL GROUP BY v.ProductEntityId, v.VariantId HAVING COUNT(*) > 1"),
        };

        var found = new List<string>();
        using var connection = new SqliteConnection(SqliteBackup.ConnectionString(databasePath, SqliteOpenMode.ReadWrite));
        connection.Open();
        foreach (var (what, sql) in checks)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                found.Add($"{what} '{reader.GetValue(0)}' is used {reader.GetInt64(1)} times");
            }
        }

        return found;
    }

    /// <summary>
    /// The plain-text message of a database rule (trigger) that refused the save, e.g.
    /// "Product ID and name are required." Null when the error was not one of our rules.
    /// </summary>
    public static string? RuleMessage(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqliteException { SqliteErrorCode: 19 } sqlite)
            {
                var start = sqlite.Message.IndexOf("RULE:", StringComparison.Ordinal);
                if (start >= 0)
                {
                    // SQLite wraps the text as: SQLite Error 19: 'RULE: <text>'.
                    var text = sqlite.Message[(start + "RULE:".Length)..].Trim();
                    if (text.EndsWith("'."))
                        text = text[..^2];
                    return text.TrimEnd('\'').Trim();
                }
            }
        }

        return null;
    }

    /// <summary>Message for a failed save of a customer, supplier, product or variant.</summary>
    public static string SaveMessage(Exception ex, string what, string idLabel) =>
        IsUniqueViolation(ex)
            ? $"This {idLabel} already exists (upper/lower case does not matter). Please use a different {idLabel}."
            : RuleMessage(ex) is { } rule
                ? rule
                : $"Could not save the {what}. {ex.GetBaseException().Message}";
}
