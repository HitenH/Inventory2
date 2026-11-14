using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service.Import;

/// <summary>The checked records of a clean preview, ready to save. Only the import service looks inside.</summary>
internal sealed class ImportPayload
{
    public List<CustomerEntity> Customers { get; init; } = [];
    public List<SupplierEntity> Suppliers { get; init; } = [];
    public List<ProductDraft> Products { get; init; } = [];
}

/// <summary>Reads, checks and saves import files (batch 8). See <see cref="IMasterImportService"/>.</summary>
public class MasterImportService(IDbContextFactory<AppDbContext> factory, ILogger<MasterImportService> logger, IConfiguration configuration)
    : IMasterImportService
{
    public async Task<ImportPreview> PreviewAsync(ImportKind kind, Stream file, string fileName)
    {
        try
        {
            var table = await ImportFileReader.ReadAsync(file, fileName);
            var mapped = ImportChecker.MapHeadings(kind, table);

            // Wrong or missing headings: show the rows as they are, but do not check them (they would all be wrong).
            if (mapped.FileIssues.Count > 0)
            {
                return new ImportPreview
                {
                    Kind = kind,
                    FileName = fileName,
                    FileIssues = mapped.FileIssues,
                    Rows = mapped.Rows.Select(r => new ImportPreviewRow(r.RowNumber, r.Cells, [])).ToList(),
                };
            }

            await using var context = await factory.CreateDbContextAsync();
            List<ImportPreviewRow> rows;
            ImportPayload? payload = null;
            int records, variants = 0;

            if (kind == ImportKind.Products)
            {
                var productIds = await context.Products.AsNoTracking().Select(p => p.ProductId).ToListAsync();
                var categoryIds = await context.Categories.AsNoTracking().Select(c => c.CategoryId).ToListAsync();
                rows = ImportChecker.CheckProducts(mapped.Rows, productIds, categoryIds);
                var drafts = ImportChecker.BuildProducts(mapped.Rows);
                records = drafts.Count;
                variants = drafts.Sum(d => d.Product.Variants?.Count ?? 0);
                payload = new ImportPayload { Products = drafts };
            }
            else
            {
                var maxMobiles = MaxMobiles(kind);
                var existing = kind == ImportKind.Customers
                    ? await context.Customers.AsNoTracking().Select(c => c.CustomerId).ToListAsync()
                    : await context.Suppliers.AsNoTracking().Select(s => s.SupplierId).ToListAsync();
                rows = ImportChecker.CheckParties(kind, mapped.Rows, existing, maxMobiles);
                records = mapped.Rows.Count;
                payload = kind == ImportKind.Customers
                    ? new ImportPayload { Customers = ImportChecker.BuildCustomers(mapped.Rows) }
                    : new ImportPayload { Suppliers = ImportChecker.BuildSuppliers(mapped.Rows) };
            }

            var clean = rows.All(r => r.Issues.Count == 0);
            return new ImportPreview
            {
                Kind = kind,
                FileName = fileName,
                Rows = rows,
                RecordCount = records,
                VariantCount = variants,
                Payload = clean ? payload : null,
            };
        }
        catch (ImportFileException ex)
        {
            return FileProblem(kind, fileName, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import preview failed for {Kind} file {File}", kind, fileName);
            return FileProblem(kind, fileName, "The file could not be read. Check that it is a .csv or .xlsx file saved from the sample file, and try again.");
        }
    }

    public async Task<int> ImportAsync(ImportPreview preview)
    {
        if (!preview.CanImport || preview.Payload is not ImportPayload payload)
            throw new ImportException("This file has problems, so it cannot be imported. Correct the file and choose it again.");

        await using var context = await factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var count = await AddAsync(context, preview.Kind, payload);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            logger.LogInformation("Imported {Count} {Noun}{Variants} from file {File}", count,
                ImportTemplates.Noun(preview.Kind, count),
                preview.Kind == ImportKind.Products ? $" with {preview.VariantCount} variants" : "", preview.FileName);
            return count;
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "Import of {File} was refused by the database", preview.FileName);
            var message = DbErrors.IsUniqueViolation(ex)
                ? "One of the IDs in the file already exists in the database (upper/lower case does not matter)."
                : DbErrors.RuleMessage(ex) ?? $"The database could not save the file. {ex.GetBaseException().Message}";
            throw new ImportException($"{message} Nothing was imported.");
        }
    }

    /// <summary>Checks once more that the IDs are still free (the data may have changed since the preview), then adds everything.</summary>
    private static async Task<int> AddAsync(AppDbContext context, ImportKind kind, ImportPayload payload)
    {
        switch (kind)
        {
            case ImportKind.Customers:
                var customerIds = await context.Customers.AsNoTracking().Select(c => c.CustomerId).ToListAsync();
                ThrowIfTaken("Customer ID", payload.Customers.Select(c => c.CustomerId), customerIds, kind);
                context.Customers.AddRange(payload.Customers);
                return payload.Customers.Count;

            case ImportKind.Suppliers:
                var supplierIds = await context.Suppliers.AsNoTracking().Select(s => s.SupplierId).ToListAsync();
                ThrowIfTaken("Supplier ID", payload.Suppliers.Select(s => s.SupplierId), supplierIds, kind);
                context.Suppliers.AddRange(payload.Suppliers);
                return payload.Suppliers.Count;

            default:
                var productIds = await context.Products.AsNoTracking().Select(p => p.ProductId).ToListAsync();
                ThrowIfTaken("Product ID", payload.Products.Select(p => p.Product.ProductId), productIds, kind);

                // Tracked categories, so each product links to the existing row instead of creating a new one.
                var categories = await context.Categories.ToListAsync();
                foreach (var draft in payload.Products)
                {
                    if (draft.CategoryId == null)
                        continue;
                    draft.Product.Category = categories.FirstOrDefault(c => string.Equals(c.CategoryId, draft.CategoryId, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ImportException($"Category ID '{draft.CategoryId}' no longer exists. Add the category first. Nothing was imported.");
                }

                context.Products.AddRange(payload.Products.Select(p => p.Product));
                return payload.Products.Count;
        }
    }

    private static void ThrowIfTaken(string label, IEnumerable<string?> wanted, IEnumerable<string?> existing, ImportKind kind)
    {
        var taken = new HashSet<string>(existing.Where(e => e != null)!, StringComparer.OrdinalIgnoreCase);
        var clashes = wanted.Where(w => w != null && taken.Contains(w)).ToList();
        if (clashes.Count == 0)
            return;

        var text = clashes.Count == 1
            ? $"{label} '{clashes[0]}' already exists."
            : $"{clashes.Count} {ImportTemplates.Noun(kind, 2)} already exist ({string.Join(", ", clashes.Take(3))}{(clashes.Count > 3 ? $" and {clashes.Count - 3} more" : "")}).";
        throw new ImportException($"{text} Nothing was imported.");
    }

    private int MaxMobiles(ImportKind kind)
    {
        var key = kind == ImportKind.Customers ? "CustomerNumberMobile" : "SupplierNumberMobile";
        return int.TryParse(configuration[key], out var value) && value > 0 ? value : ImportChecker.DefaultMaxMobiles;
    }

    private static ImportPreview FileProblem(ImportKind kind, string fileName, string message) => new()
    {
        Kind = kind,
        FileName = fileName,
        FileIssues = [new ImportIssue(null, null, message)],
    };
}
