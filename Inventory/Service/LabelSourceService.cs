using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>A purchase's labels: one line per purchase line as entered, and the purchase's name for the activity log.</summary>
public sealed record PurchaseLabels(Guid PurchaseId, string Label, List<LabelItem> Items);

/// <summary>
/// Version 3.0, stage L, batch L-2 (Hiten, 25 Nov 2026): where label lists come from, and how far the current sheet is used.
/// <list type="bullet">
/// <item>From a purchase: one line per purchase line in the order entered (the same product and variant twice stays twice),
/// copies = quantity, lines with a quantity of 0 or less (returns, damage) left out.</item>
/// <item>"Already used on this sheet" is kept in the database per label set-up, so it survives a restart, a cleared browser
/// and a restore of a backup.</item>
/// </list>
/// Future rule (Hiten, 25 Nov 2026, ROADMAP stage L): labels will be tied to purchases, and extra labels need permission from
/// someone higher up, against duplicate labels and labels of a cheaper product on a dearer one. Every print is already in the
/// activity log (ActivityLogService.Labels).
/// </summary>
public class LabelSourceService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<PurchaseLabels?> ForPurchaseAsync(Guid purchaseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var purchase = await db.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == purchaseId);
        if (purchase is null)
        {
            return null;
        }

        var lines = await (from line in db.PurchaseVariant.AsNoTracking()
                           where line.PurchaseEntityId == purchaseId && line.Quantity > 0
                           join product in db.Products.AsNoTracking() on line.ProductEntityId equals product.Id
                           join variant in db.Variants.AsNoTracking() on line.VariantEntityId equals variant.Id
                           orderby line.SerialNumber
                           select new { product.ProductId, ProductName = product.Name, variant.VariantId, VariantName = variant.Name, line.Quantity })
                          .ToListAsync();
        var items = lines.Select(l => new LabelItem(l.ProductId ?? "", l.ProductName ?? "", l.VariantId ?? "", l.VariantName ?? "", l.Quantity ?? 0)).ToList();
        return new PurchaseLabels(purchaseId, $"Purchase {purchase.VoucherId} of {purchase.Date:dd-MM-yyyy}", items);
    }

    public async Task<int> UsedAsync(int layoutId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.LabelSheetUse.AsNoTracking().Where(u => u.LabelLayoutId == layoutId).Select(u => u.Used).FirstOrDefaultAsync();
    }

    public async Task SetUsedAsync(int layoutId, int used)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.LabelSheetUse.FirstOrDefaultAsync(u => u.LabelLayoutId == layoutId);
        if (row is null)
        {
            db.LabelSheetUse.Add(new LabelSheetUseEntity { LabelLayoutId = layoutId, Used = used });
        }
        else
        {
            row.Used = used;
        }

        await db.SaveChangesAsync();
    }
}
