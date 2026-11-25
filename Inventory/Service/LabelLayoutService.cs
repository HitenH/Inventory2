using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>
/// Version 3.0, stage L (Hiten, 25 Nov 2026): the named label set-ups ("A4 65 Label" first). Anyone who may print labels
/// chooses which one is in use; only the "Change label settings" switch may add, change or delete one. Every change is
/// stamped and written to the activity log by the save step, like any other record.
/// </summary>
public class LabelLayoutService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<LabelSheetLayout>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.LabelLayouts.AsNoTracking().OrderBy(l => l.Name).ToListAsync();
        db.MarkSeen(rows);   // batch 1c-2: someone else changing the same label meanwhile is caught at save
        return rows.Select(ToLayout).ToList();
    }

    /// <summary>The label in use; the first one (A4 65 Label) if none is marked, so printing always has a layout.</summary>
    public async Task<LabelSheetLayout> ActiveAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.LabelLayouts.AsNoTracking().OrderByDescending(l => l.IsActive).ThenBy(l => l.Id).FirstOrDefaultAsync();
        return row is null ? LabelSheetLayout.A4Sheet65 : ToLayout(row);
    }

    /// <summary>Switch the label in use (needs "Print labels").</summary>
    public async Task<UserAdminResult> UseAsync(int id, UserAccess access)
    {
        if (!access.Allows(Permissions.LabelsPrint))
        {
            return UserAdminResult.Refuse("You may not print labels.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.LabelLayouts.ToListAsync();
        var chosen = rows.FirstOrDefault(r => r.Id == id);
        if (chosen is null)
        {
            return UserAdminResult.Refuse("That label was not found. Reload the page.");
        }

        foreach (var row in rows)
        {
            row.IsActive = row.Id == id;
        }

        return await UserAdmin.TrySaveAsync(db) ?? UserAdminResult.Success($"Now printing on {chosen.Name}.");
    }

    /// <summary>Add (Id 0) or change a label set-up (needs "Change label settings"). Refused with plain reasons if the numbers do not fit.</summary>
    public async Task<UserAdminResult> SaveAsync(LabelSheetLayout layout, UserAccess access)
    {
        if (!access.Allows(Permissions.LabelsSettings))
        {
            return UserAdminResult.Refuse("You may not change label settings.");
        }

        var problems = layout.Problems();
        if (problems.Count > 0)
        {
            return UserAdminResult.Refuse(string.Join(" ", problems));
        }

        await using var db = await factory.CreateDbContextAsync();
        var name = layout.Name.Trim();
        var lower = name.ToLower();
        if (await db.LabelLayouts.AnyAsync(l => l.Id != layout.Id && l.Name.ToLower() == lower))
        {
            return UserAdminResult.Refuse($"There is already a label called {name}.");
        }

        LabelLayoutEntity row;
        if (layout.Id == 0)
        {
            row = new LabelLayoutEntity { IsActive = !await db.LabelLayouts.AnyAsync() };
            db.LabelLayouts.Add(row);
        }
        else
        {
            row = await db.LabelLayouts.FirstOrDefaultAsync(l => l.Id == layout.Id)
                  ?? throw new InvalidOperationException("That label was not found. Reload the page.");
        }

        Copy(layout with { Name = name }, row);
        return await UserAdmin.TrySaveAsync(db) ?? UserAdminResult.Success($"Label {name} saved.");
    }

    /// <summary>Delete a label set-up (needs "Change label settings"); the one in use and the last one stay.</summary>
    public async Task<UserAdminResult> DeleteAsync(int id, UserAccess access)
    {
        if (!access.Allows(Permissions.LabelsSettings))
        {
            return UserAdminResult.Refuse("You may not change label settings.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var row = await db.LabelLayouts.FirstOrDefaultAsync(l => l.Id == id);
        if (row is null)
        {
            return UserAdminResult.Refuse("That label was not found. Reload the page.");
        }

        if (row.IsActive)
        {
            return UserAdminResult.Refuse("This label is in use. Choose another label first.");
        }

        if (await db.LabelLayouts.CountAsync() <= 1)
        {
            return UserAdminResult.Refuse("The last label cannot be deleted.");
        }

        db.LabelLayouts.Remove(row);
        return await UserAdmin.TrySaveAsync(db) ?? UserAdminResult.Success($"Label {row.Name} deleted.");
    }

    public static LabelSheetLayout ToLayout(LabelLayoutEntity r) =>
        new(r.Name, r.PageWidthMm, r.PageHeightMm, r.Columns, r.Rows, r.LabelWidthMm, r.LabelHeightMm, r.TopMm, r.LeftMm)
        {
            Id = r.Id, IsActive = r.IsActive, GapAcrossMm = r.GapAcrossMm, GapDownMm = r.GapDownMm,
            ContentDownMm = r.ContentDownMm, ContentLeftMm = r.ContentLeftMm, QrMm = r.QrMm,
            NameFontPt = r.NameFontPt, VariantFontPt = r.VariantFontPt, IdFontPt = r.IdFontPt,
        };

    public static void Copy(LabelSheetLayout l, LabelLayoutEntity r)
    {
        r.Name = l.Name;
        r.PageWidthMm = l.PageWidthMm; r.PageHeightMm = l.PageHeightMm;
        r.Columns = l.Columns; r.Rows = l.Rows;
        r.LabelWidthMm = l.LabelWidthMm; r.LabelHeightMm = l.LabelHeightMm;
        r.GapAcrossMm = l.GapAcrossMm; r.GapDownMm = l.GapDownMm;
        r.TopMm = l.TopMm; r.LeftMm = l.LeftMm;
        r.ContentDownMm = l.ContentDownMm; r.ContentLeftMm = l.ContentLeftMm;
        r.QrMm = l.QrMm; r.NameFontPt = l.NameFontPt; r.VariantFontPt = l.VariantFontPt; r.IdFontPt = l.IdFontPt;
    }
}
