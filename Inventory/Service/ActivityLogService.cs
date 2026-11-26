using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>
/// One save (or the whole creation of a record) in the activity panel: when, who, its lines, and the line-by-line
/// details folded under a summary ("show lines"), so a 100-line purchase does not fill the panel.
/// </summary>
public sealed record ActivityGroup(DateTime At, string? By, IReadOnlyList<string> Lines, IReadOnlyList<string> Details, DateTime? Until = null)
{
    public ActivityGroup(DateTime at, string? by, IReadOnlyList<string> lines) : this(at, by, lines, []) { }

    /// <summary>"25-11-2026 12:30", or "25-11-2026 12:30–12:45" for a sitting that went on for a while.</summary>
    public string When => Until is { } end && end.ToString("HH:mm") != At.ToString("HH:mm")
        ? $"{At:dd-MM-yyyy HH:mm}–{(end.Date == At.Date ? end.ToString("HH:mm") : end.ToString("dd-MM-yyyy HH:mm"))}"
        : At.ToString("dd-MM-yyyy HH:mm");
}

/// <summary>Batch 1c-5: the kinds offered by the Kind filter of the Activity log screen.</summary>
public enum ActivityKind { Everything, Customers, Suppliers, Products, PurchaseOrders, Purchases, SalesOrders, Sales, ReturnsAndDamage, UsersAndRoles, Logins, Backups, Imports, Labels }

/// <summary>What the Activity log screen asks for. Person null = everyone.</summary>
public sealed record ActivityFilter(DateOnly From, DateOnly To, string? Person, ActivityKind Kind, string? Search);

/// <summary>One row of the Activity log screen: a sitting (or a record's creation), its record, its shade, and a link if the record still exists.</summary>
public sealed record ActivityRow(ActivityGroup Group, string? RecordType, string? RecordId, string? RecordLabel, string Kind, string? Link);

/// <summary>A record's history as one person may see it.</summary>
public sealed record RecordHistory(IReadOnlyList<ActivityGroup> Groups, bool EarlierNotRecorded, string? Label = null);

/// <summary>
/// Version 3.0, batches 1c-3 and 1c-4: writes the activity-log lines that are not a save of a record
/// (logins, backups, imports are noted by the import itself), and reads a record's history for the
/// "See activity log" panel. Reading needs the "See activity log" switch; lines with a rate follow the
/// rate switches, so a hidden rate is never sent to that person's screen.
/// </summary>
public class ActivityLogService(IDbContextFactory<AppDbContext> factory, CurrentUser currentUser)
{
    public const string Login = "Login";
    public const string LabelsPrinted = "Labels";
    public const string LoginRefused = "Login refused";
    public const string Backup = "Backup";

    /// <summary>Writes one line. <paramref name="by"/> defaults to the person logged in on this tab.</summary>
    public async Task WriteAsync(string action, string text, string? by = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.ActivityLog.Add(new ActivityEntry
        {
            At = DateTime.Now,
            By = by ?? currentUser.Name,
            SaveId = Guid.NewGuid(),
            Action = action,
            Text = text,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Like <see cref="WriteAsync"/>, but a failure only goes to the log file: it must never stop a login or a backup.</summary>
    public async Task TryWriteAsync(string action, string text, string? by, ILogger logger)
    {
        try
        {
            await WriteAsync(action, text, by);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write to the activity log: {Action} {Text}", action, text);
        }
    }

    /// <summary>
    /// Stage L: one line for a print of labels. A print made from a purchase is filed under that purchase, so it shows in the
    /// purchase's activity panel. A failure only goes to the log file: it must never stop printing.
    /// </summary>
    public async Task TryWriteLabelsAsync(string text, PurchaseLabels? purchase, ILogger logger)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            db.ActivityLog.Add(new ActivityEntry
            {
                At = DateTime.Now, By = currentUser.Name, SaveId = Guid.NewGuid(), Action = LabelsPrinted, Text = text,
                RecordType = purchase is null ? null : nameof(PurchaseEntity),
                RecordId = purchase?.PurchaseId.ToString(),
                RecordLabel = purchase?.Label,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write the label print to the activity log: {Text}", text);
        }
    }

    /// <summary>The history of one record, newest first, as this person may see it.</summary>
    public async Task<RecordHistory> ForRecordAsync(string recordType, string recordId, UserAccess access)
    {
        if (!access.Allows(Permissions.ActivitySee))
        {
            return new RecordHistory([], false);
        }

        await using var db = await factory.CreateDbContextAsync();
        var id = recordId.ToLowerInvariant();
        var rows = await db.ActivityLog.AsNoTracking()
            .Where(a => a.RecordType == recordType && a.RecordId != null && a.RecordId.ToLower() == id)
            .OrderByDescending(a => a.At).ThenBy(a => a.Id)
            .ToListAsync();
        var created = rows.Any(r => r.Action == "Created");
        var visible = rows.Where(r => MaySee(r, access)).ToList();
        // Owner, 25 Nov 2026: everything done while creating the record is one entry, its lines folded;
        // later saves are shown in full, except that more than FoldAbove added lines are folded too (Bulk Entry).
        var creation = visible.Where(r => r.Action == "Created").OrderBy(r => r.Id).ToList();
        var groups = visible.Where(r => r.Action != "Created")
            .GroupBy(r => r.SaveId)
            .Select(g => Group(g.OrderBy(r => r.Id).ToList(), fold: false))
            .ToList();
        if (creation.Count > 0)
        {
            groups.Add(Group(creation, fold: true));
        }

        groups = groups.OrderByDescending(g => g.At).ToList();
        // The newest name the log knows (a renamed customer shows its current name).
        return new RecordHistory(groups, !created, rows.FirstOrDefault()?.RecordLabel);
    }

    /// <summary>The "record id" the damage report's "See activity log" button passes (Hiten, 26 Nov 2026: a button like every other screen).</summary>
    public const string DamageReportId = "damage-report";

    /// <summary>What the shop calls the stock changes the damage report deals with (labels start with these).</summary>
    private static readonly string[] DamageLabels = ["Damage ", "Repair ", "Write-off ", "Purchase return "];

    /// <summary>
    /// Stage 2, batch 2-5: the history behind the damage report - damage recorded or taken back, suppliers assigned, ready to
    /// send, repairs, write-offs and purchase returns - newest first, the newest 300 entries, each with the record it belongs to.
    /// </summary>
    public async Task<RecordHistory> ForDamageReportAsync(UserAccess access)
    {
        if (!access.Allows(Permissions.ActivitySee))
        {
            return new RecordHistory([], false);
        }

        await using var db = await factory.CreateDbContextAsync();
        var rows = (await db.ActivityLog.AsNoTracking()
                .Where(a => a.RecordType == nameof(StockChangeEntity) && a.RecordLabel != null)
                .OrderByDescending(a => a.At).ThenBy(a => a.Id)
                .ToListAsync())
            .Where(a => DamageLabels.Any(p => a.RecordLabel!.StartsWith(p, StringComparison.Ordinal)) && MaySee(a, access))
            .ToList();
        var groups = rows
            .GroupBy(r => r.Action == "Created" ? $"created|{r.RecordId}" : $"{r.SaveId}|{r.RecordId}")
            .Select(g =>
            {
                var group = Group(g.OrderBy(r => r.Id).ToList(), fold: g.First().Action == "Created");
                var lines = group.Lines.ToList();
                lines[0] = $"{g.First().RecordLabel} – {lines[0]}";
                return group with { Lines = lines };
            })
            .OrderByDescending(g => g.At)
            .Take(300)
            .ToList();
        return new RecordHistory(groups, false, "Damage report");
    }

    /// <summary>More added lines than this in one save are folded under a summary.</summary>
    public const int FoldAbove = 3;

    private static ActivityGroup Group(List<ActivityEntry> rows, bool fold)
    {
        var details = rows.Where(r => r.IsLineDetail).ToList();
        var added = details.Where(r => r.Pieces is not null).ToList();
        var lines = rows.Where(r => !r.IsLineDetail).Select(r => r.Text).ToList();
        if (added.Count == 0 || (!fold && added.Count <= FoldAbove))
        {
            return new ActivityGroup(rows[0].At, rows[0].By, rows.Select(r => r.Text).ToList(), [], rows[^1].At);
        }

        var pieces = added.Sum(r => (long)r.Pieces!.Value);
        lines.Add($"{added.Count} line{(added.Count == 1 ? "" : "s")} added{(fold ? " while creating" : "")}, {pieces} piece{(pieces == 1 ? "" : "s")}");
        return new ActivityGroup(rows[0].At, rows[0].By, lines, details.Select(r => r.Text).ToList(), rows[^1].At);
    }

    // ---- Version 3.0, batch 1c-5: the Activity log screen (mock-up agreed with Hiten, 25 Nov 2026). ----

    /// <summary>
    /// The whole log for a period, as this person may see it: one row per sitting (or per record's creation),
    /// folded and rate-hidden exactly as in the record panel, newest first. Search looks only at what the person may see.
    /// </summary>
    public async Task<List<ActivityRow>> SearchAsync(ActivityFilter filter, UserAccess access)
    {
        if (!access.Allows(Permissions.ActivitySee))
        {
            return [];
        }

        await using var db = await factory.CreateDbContextAsync();
        var from = filter.From.ToDateTime(TimeOnly.MinValue);
        var to = filter.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var query = db.ActivityLog.AsNoTracking().Where(a => a.At >= from && a.At < to);
        if (!string.IsNullOrWhiteSpace(filter.Person))
        {
            var person = filter.Person.Trim().ToLower();
            query = query.Where(a => a.By != null && a.By.ToLower() == person);
        }

        query = filter.Kind switch
        {
            ActivityKind.Everything => query,
            ActivityKind.Logins => query.Where(a => a.Action == Login || a.Action == LoginRefused),
            ActivityKind.Backups => query.Where(a => a.Action == Backup),
            ActivityKind.Imports => query.Where(a => a.Action == "Import"),
            ActivityKind.Labels => query.Where(a => a.Action == LabelsPrinted),
            _ => query.Where(a => a.RecordType != null && RecordTypesOf(filter.Kind).Contains(a.RecordType)),
        };

        var rows = (await query.OrderBy(a => a.Id).ToListAsync()).Where(r => MaySee(r, access)).ToList();
        var result = rows
            .GroupBy(r => r.Action == "Created" && r.RecordId != null ? $"created|{r.RecordType}|{r.RecordId}" : r.SaveId.ToString())
            .Select(g =>
            {
                var list = g.OrderBy(r => r.Id).ToList();
                var first = list[0];
                return new ActivityRow(Group(list, fold: first.Action == "Created"), first.RecordType, first.RecordId, first.RecordLabel, KindOf(first), null);
            })
            .Where(r => Matches(r, filter.Search))
            .OrderByDescending(r => r.Group.At)
            .ToList();

        return await WithLinksAsync(db, result, access);
    }

    /// <summary>The people who appear in the log or have a login, for the Person filter.</summary>
    public async Task<List<string>> PeopleAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking().Select(u => u.Name).ToListAsync();
        var inLog = await db.ActivityLog.AsNoTracking().Where(a => a.By != null).Select(a => a.By!).Distinct().ToListAsync();
        return users.Concat(inLog).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<int> CountAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ActivityLog.CountAsync();
    }

    /// <summary>
    /// Erases the whole log (Hiten, 25 Nov 2026: Admin role only, everything at once, no trace left; single entries
    /// cannot be removed). Done straight in the database, so not even the erase itself is written. Returns how many went.
    /// </summary>
    public async Task<int> EraseAllAsync(UserAccess access)
    {
        if (!access.HasAdminRole)
        {
            throw new UnauthorizedAccessException("Only the Admin role may erase the activity log.");
        }

        await using var db = await factory.CreateDbContextAsync();
        return await db.ActivityLog.ExecuteDeleteAsync();
    }

    private static bool Matches(ActivityRow row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var s = search.Trim();
        bool Has(string? text) => text is not null && text.Contains(s, StringComparison.OrdinalIgnoreCase);
        return Has(row.RecordLabel) || Has(row.Group.By) || row.Group.Lines.Any(Has) || row.Group.Details.Any(Has);
    }

    private static string[] RecordTypesOf(ActivityKind kind) => kind switch
    {
        ActivityKind.Customers => [nameof(CustomerEntity)],
        ActivityKind.Suppliers => [nameof(SupplierEntity)],
        ActivityKind.Products => [nameof(ProductEntity), nameof(CategoryEntity)],
        ActivityKind.PurchaseOrders => [nameof(PurchaseOrderEntity)],
        ActivityKind.Purchases => [nameof(PurchaseEntity)],
        ActivityKind.SalesOrders => [nameof(SalesOrderEntity)],
        ActivityKind.Sales => [nameof(SalesEntity)],
        ActivityKind.ReturnsAndDamage => [nameof(StockChangeEntity)],   // stage 2
        ActivityKind.UsersAndRoles => [nameof(UserEntity), nameof(RoleEntity)],
        _ => [],
    };

    /// <summary>The shade of the row's left stripe (site.css, act-kind-*).</summary>
    public static string KindOf(ActivityEntry entry) => entry.RecordType switch
    {
        nameof(PurchaseEntity) => "purchase",
        nameof(PurchaseOrderEntity) => "purchase-order",
        nameof(SalesEntity) => "sale",
        nameof(SalesOrderEntity) => "sales-order",
        nameof(CustomerEntity) or nameof(SupplierEntity) => "party",
        nameof(ProductEntity) or nameof(CategoryEntity) => "product",
        nameof(UserEntity) or nameof(RoleEntity) => "people",
        nameof(StockChangeEntity) => "stock",   // stage 2: sales returns, damage ...
        _ => "system",
    };

    /// <summary>Pale tints, none red, ordered so that neighbours differ clearly (blue, amber, green, violet, teal ...).</summary>
    private static readonly int[] Hues = [210, 35, 150, 280, 185, 320, 95, 245, 55, 165];

    /// <summary>
    /// Each user's tint, given in the order users were added, so the first ten people always look clearly different
    /// (Hiten saw Admin and Hiten in nearly the same gold when tints came from the name alone, 25 Nov 2026).
    /// </summary>
    public async Task<Dictionary<string, int>> PersonHuesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var names = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < names.Count; i++)
        {
            map.TryAdd(names[i], Hues[i % Hues.Length]);
        }

        return map;
    }

    /// <summary>
    /// The tint of a name that is not a user (any more): always the same for the same name (own hash, not .NET's, which changes
    /// between runs), picked from hues that are not red (red means Delete in this app).
    /// </summary>
    public static int PersonHue(string? name)
    {
        int[] hues = Hues;
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;   // the app itself: shown grey
        }

        var hash = 17;
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            hash = unchecked(hash * 31 + ch);
        }

        return hues[(hash & int.MaxValue) % hues.Length];
    }

    /// <summary>A link to the record when it still exists and the person may open its screen.</summary>
    private static async Task<List<ActivityRow>> WithLinksAsync(AppDbContext db, List<ActivityRow> rows, UserAccess access)
    {
        var guids = rows.Where(r => r.RecordId is not null).Select(r => Guid.TryParse(r.RecordId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
        var existing = new HashSet<Guid>();
        existing.UnionWith(await db.Customers.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        existing.UnionWith(await db.Suppliers.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        existing.UnionWith(await db.Products.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        existing.UnionWith(await db.Purchases.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        existing.UnionWith(await db.SalesOrders.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        existing.UnionWith(await db.Sales.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        existing.UnionWith(await db.PurchaseOrders.Where(x => guids.Contains(x.Id)).Select(x => x.Id).ToListAsync());
        // Stage 2: sales and purchase returns have their own screens; other stock changes (damage, repairs ...) are on the damage report.
        var stockKinds = await db.StockChanges.Where(x => guids.Contains(x.Id)).Include(x => x.Lines).ToDictionaryAsync(x => x.Id);
        existing.UnionWith(stockKinds.Keys);

        return rows.Select(r =>
        {
            string? path = r.RecordType switch
            {
                nameof(CustomerEntity) => $"/customer/{r.RecordId}",
                nameof(SupplierEntity) => $"/supplier/{r.RecordId}",
                nameof(ProductEntity) => $"/product/{r.RecordId}",
                nameof(PurchaseEntity) => $"/purchase/{r.RecordId}",
                nameof(SalesOrderEntity) => $"/salesorder/{r.RecordId}",
                nameof(SalesEntity) => $"/sale/{r.RecordId}",
                nameof(PurchaseOrderEntity) => "/purchaseorder",
                nameof(UserEntity) => "/users",
                nameof(RoleEntity) => "/roles",
                nameof(StockChangeEntity) when Guid.TryParse(r.RecordId, out var sc) && stockKinds.TryGetValue(sc, out var k) => k.Kind switch
                {
                    StockChangeKind.SalesReturn => $"/salesreturn/{r.RecordId}",
                    StockChangeKind.PurchaseReturn => $"/purchasereturn/{r.RecordId}",
                    StockChangeKind.Move or StockChangeKind.MoveAtOnce or StockChangeKind.StartingCount => $"/transfer/{r.RecordId}",   // stage 3, batch 3-5
                    _ when StockEntryService.TypeOf(k) is not null => $"/stockchange/{r.RecordId}",   // batch 2-7
                    _ => "/damage",
                },
                _ => null,
            };
            var exists = r.RecordType is nameof(UserEntity) or nameof(RoleEntity)
                || (Guid.TryParse(r.RecordId, out var g) && existing.Contains(g));
            return path is not null && exists && access.AllowsPath(path) ? r with { Link = path } : r;
        }).ToList();
    }

    /// <summary>A line with a rate is shown only to people who may see that side's rates.</summary>
    public static bool MaySee(ActivityEntry entry, UserAccess access) => entry.RateSide switch
    {
        ActivityLogWriter.PurchaseSide => access.SeesPurchaseRates,
        ActivityLogWriter.SaleSide => access.SeesSaleRates,
        _ => true,
    };
}
