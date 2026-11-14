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
    public List<PurchaseDraft> Purchases { get; init; } = [];
}

/// <summary>Reads, checks and saves import files (batch 8). See <see cref="IMasterImportService"/>.</summary>
/// <remarks>
/// <paramref name="backupService"/> is optional so tests can leave it out (no backup is taken then). The app always supplies it,
/// and a purchases import takes a "before import" backup with it before saving anything.
/// </remarks>
public class MasterImportService(IDbContextFactory<AppDbContext> factory, ILogger<MasterImportService> logger, IConfiguration configuration,
    BackupService? backupService = null)
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
            int records, variants = 0, lines = 0;

            if (kind == ImportKind.Purchases)
            {
                var supplierIds = await context.Suppliers.AsNoTracking().Select(s => s.SupplierId).ToListAsync();
                var known = (await context.Products.AsNoTracking()
                        .Select(p => new { p.ProductId, Variants = p.Variants!.Select(v => v.VariantId).ToList() }).ToListAsync())
                    .Where(p => p.ProductId != null)
                    .Select(p => new KnownProduct(p.ProductId!, p.Variants.Where(v => v != null).Select(v => v!).ToList()))
                    .ToList();
                var existingPurchases = await ExistingPurchasesAsync(context);
                rows = ImportChecker.CheckPurchases(mapped.Rows, supplierIds, known, existingPurchases);
                if (rows.All(r => r.Issues.Count == 0))
                {
                    var drafts = ImportChecker.BuildPurchases(mapped.Rows);
                    records = drafts.Count;
                    lines = drafts.Sum(d => d.Lines.Count);
                    payload = new ImportPayload { Purchases = drafts };
                }
                else
                {
                    records = 0;   // the counts are shown only for a clean file
                }
            }
            else if (kind == ImportKind.Products)
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
                LineCount = lines,
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

        // Stock goes up as soon as purchases are saved, so a copy of the database is made first. It is made before the
        // transaction starts (the backup reads the database on its own connection). If it fails, nothing is imported.
        if (preview.Kind == ImportKind.Purchases && backupService != null)
        {
            try
            {
                var backupPath = await backupService.BackupBeforeImportAsync();
                logger.LogInformation("Safety backup before importing {File}: {Path}", preview.FileName, backupPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Safety backup before importing {File} failed", preview.FileName);
                throw new ImportException($"The safety backup could not be made, so nothing was imported. {ex.Message}");
            }
        }

        await using var context = await factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var count = await AddAsync(context, preview.Kind, payload);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            if (preview.Kind == ImportKind.Purchases)
                logger.LogInformation("Imported {Count} {Noun} with {Lines} lines from file {File}", count,
                    ImportTemplates.Noun(preview.Kind, count), preview.LineCount, preview.FileName);
            else
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

            case ImportKind.Purchases:
                return await AddPurchasesAsync(context, payload.Purchases);

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

    /// <summary>
    /// Checks once more that the suppliers, products and variants still exist and that no purchase for the same supplier and
    /// date has appeared, then adds the purchases. Voucher numbers continue after the last one of the date (as the Purchase
    /// screen does); lines are numbered 1..n in file order; line amounts and the total are worked out as the screen does.
    /// </summary>
    /// <summary>Every purchase in the database with its items, for the "exact copy" check and the voucher numbering.</summary>
    private static async Task<List<ExistingPurchase>> ExistingPurchasesAsync(AppDbContext context) =>
        (await context.Purchases.AsNoTracking()
            .Select(p => new
            {
                p.Supplier!.SupplierId, p.Date, p.VoucherId,
                Lines = p.PurchaseVariants!.Select(l => new { l.Product!.ProductId, l.ProductVariant!.VariantId, l.Quantity }).ToList(),
            }).ToListAsync())
        .Where(p => p.SupplierId != null)
        .Select(p => new ExistingPurchase(p.SupplierId!, p.Date, p.VoucherId,
            p.Lines.Select(l => new ExistingLine(l.ProductId ?? "", l.VariantId ?? "", l.Quantity ?? 0)).ToList()))
        .ToList();

    private static async Task<int> AddPurchasesAsync(AppDbContext context, List<PurchaseDraft> drafts)
    {
        var suppliers = new Dictionary<string, SupplierEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await context.Suppliers.AsNoTracking().ToListAsync())
            if (s.SupplierId != null) suppliers[s.SupplierId] = s;

        var products = new Dictionary<string, ProductEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in await context.Products.AsNoTracking().Include(p => p.Variants).ToListAsync())
            if (p.ProductId != null) products[p.ProductId] = p;

        var existing = await ExistingPurchasesAsync(context);
        var lastVoucher = existing.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.Max(e => e.VoucherId));

        var purchases = new List<PurchaseEntity>();
        foreach (var draft in drafts)
        {
            if (!suppliers.TryGetValue(draft.SupplierId, out var supplier))
                throw new ImportException($"Supplier ID '{draft.SupplierId}' no longer exists. Add the supplier first. Nothing was imported.");

            var mine = draft.Lines.Select(l => (l.ProductId, l.VariantId, l.Quantity)).ToList();
            var vouchers = existing
                .Where(e => e.Date == draft.Date && string.Equals(e.SupplierId, draft.SupplierId, StringComparison.OrdinalIgnoreCase)
                            && ImportChecker.HasSameLines(e, mine))
                .Select(e => e.VoucherId).Order().ToList();
            if (vouchers.Count > 0)
                throw new ImportException($"{ImportChecker.PurchaseExistsMessage(draft.SupplierId, draft.Date, vouchers)} Nothing was imported.");

            var voucherId = lastVoucher.GetValueOrDefault(draft.Date) + 1;
            lastVoucher[draft.Date] = voucherId;

            var purchase = new PurchaseEntity
            {
                Id = Guid.NewGuid(),
                SupplierEntityId = supplier.Id,
                Date = draft.Date,
                VoucherId = voucherId,
                Remarks = draft.Remarks,
                DiscountAmount = 0,
                PurchaseVariants = [],
            };

            var serial = 1;
            foreach (var line in draft.Lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    throw new ImportException($"Product ID '{line.ProductId}' no longer exists. Add the product first. Nothing was imported.");
                var variant = product.Variants?.FirstOrDefault(v => string.Equals(v.VariantId, line.VariantId, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ImportException($"Variant ID '{line.VariantId}' no longer exists for product '{line.ProductId}'. Nothing was imported.");

                var amount = LineAmounts.ComputeAmount(line.Rate, line.Quantity);
                purchase.PurchaseVariants.Add(new PurchaseVariant
                {
                    Id = Guid.NewGuid(),
                    SerialNumber = serial++,
                    ProductEntityId = product.Id,
                    VariantEntityId = variant.Id,
                    Quantity = line.Quantity,
                    ProductRate = line.Rate,
                    Amount = amount,
                    DiscountPercent = line.DiscountPercent,
                    AmountAfterDiscount = LineAmounts.ComputeAmountAfterDiscount(amount, line.DiscountPercent),
                });
            }

            // Same result as TransactionTotals.RecalculatePurchaseAsync: lines after discount, less the header discount (0).
            purchase.TotalAmountProduct = purchase.PurchaseVariants.Sum(l => l.AmountAfterDiscount ?? 0) - (purchase.DiscountAmount ?? 0);
            purchases.Add(purchase);
        }

        context.Purchases.AddRange(purchases);
        return purchases.Count;
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
