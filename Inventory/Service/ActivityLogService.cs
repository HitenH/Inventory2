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

    /// <summary>A line with a rate is shown only to people who may see that side's rates.</summary>
    public static bool MaySee(ActivityEntry entry, UserAccess access) => entry.RateSide switch
    {
        ActivityLogWriter.PurchaseSide => access.SeesPurchaseRates,
        ActivityLogWriter.SaleSide => access.SeesSaleRates,
        _ => true,
    };
}
