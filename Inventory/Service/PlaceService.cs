using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>Stage 3: one rack of a place, with how many items have it as a signpost.</summary>
public sealed record RackItem(int Id, int Number, int Levels, int ItemCount)
{
    /// <summary>"A–D".</summary>
    public string LevelsText => Levels == 1 ? "A" : $"A–{RackAddress.LevelLetter(Levels)}";
}

/// <summary>Stage 3: a place that counts stock (the shop, a godown) and its racks.</summary>
public sealed record PlaceItem(int Id, int Number, string Name, IReadOnlyList<RackItem> Racks)
{
    public string NumberText => Number.ToString("00");
    public bool IsStarting => Id == Places.StartingId;
}

/// <summary>Stage 3: one signpost of a variant – "kept on 02-05-A" – with the place it is in.</summary>
public sealed record VariantRackItem(int Id, Guid VariantId, int RackId, int PlaceId, string PlaceName, RackAddress Address, bool IsMain)
{
    /// <summary>"Shop, Rack 1, Level B" – the address in full words (Hiten, 26 Nov 2026).</summary>
    public string Description => RackAddress.Describe(PlaceName, Address);

    /// <summary>"Rack 1, Level B" – under a heading that already names the place.</summary>
    public string RackDescription => $"Rack {Address.Rack}, Level {Address.Level}";
}

/// <summary>Stage 3: a place or rack change the rules do not allow, in plain words for the screen.</summary>
public sealed class PlaceRefused(string message) : Exception(message);

/// <summary>
/// Version 3.0, stage 3, batch 3-2 (Hiten, 26 Nov 2026): the rule book for places, racks and where each variant is kept.
/// Places count stock; racks and levels are signposts for pickers and arrangers, not counted ("way A"). Addresses are
/// numbers except the level letter: <c>02-03-B</c>. One variant can be on several racks in several places, one rack level
/// holds many variants, and each variant has at most one main rack per place (where to look first).
/// Place and rack changes go to the activity log as settings; a variant's racks go into its product's history
/// (without stamping the product, so setting racks never clashes with someone editing the product).
/// </summary>
public class PlaceService(IDbContextFactory<AppDbContext> factory, ActivityLogService activity, CurrentUser currentUser)
{
    public async Task<List<PlaceItem>> AllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var used = await db.VariantRacks.AsNoTracking().GroupBy(v => v.RackId).Select(g => new { g.Key, Count = g.Select(v => v.VariantEntityId).Distinct().Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);
        var places = await db.Places.AsNoTracking().Include(p => p.Racks).OrderBy(p => p.Number).ToListAsync();
        return places.Select(p => new PlaceItem(p.Id, p.Number, p.Name,
            p.Racks.OrderBy(r => r.Number).Select(r => new RackItem(r.Id, r.Number, r.Levels, used.GetValueOrDefault(r.Id))).ToList())).ToList();
    }

    // ----- Places -----

    public async Task<PlaceItem> AddPlaceAsync(string? name)
    {
        var clean = Clean(name) ?? throw new PlaceRefused("Type the name of the place first.");
        await using var db = await factory.CreateDbContextAsync();
        await RefuseNameTakenAsync(db, clean, exceptId: null);
        var numbers = await db.Places.Select(p => p.Number).ToListAsync();
        var number = Enumerable.Range(1, Places.MaxNumber).FirstOrDefault(n => !numbers.Contains(n));
        if (number == 0)
        {
            throw new PlaceRefused($"There can be at most {Places.MaxNumber} places.");
        }

        var place = new PlaceEntity { Number = number, Name = clean };
        db.Places.Add(place);
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Place added: {number:00} {clean}");
        return new PlaceItem(place.Id, place.Number, place.Name, []);
    }

    public async Task RenamePlaceAsync(int id, string? name)
    {
        var clean = Clean(name) ?? throw new PlaceRefused("A place needs a name.");
        await using var db = await factory.CreateDbContextAsync();
        var place = await db.Places.FindAsync(id) ?? throw new PlaceRefused("This place is no longer there.");
        if (place.Name == clean)
        {
            return;
        }

        await RefuseNameTakenAsync(db, clean, exceptId: id);
        var old = place.Name;
        place.Name = clean;
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Place {place.Number:00} renamed: {old} → {clean}");
    }

    /// <summary>The number is the first part of every address in the place, so it can only change while the place has no racks.</summary>
    public async Task RenumberPlaceAsync(int id, int number)
    {
        if (number is < 1 or > Places.MaxNumber)
        {
            throw new PlaceRefused($"A place number is 1 to {Places.MaxNumber}.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var place = await db.Places.FindAsync(id) ?? throw new PlaceRefused("This place is no longer there.");
        if (place.Number == number)
        {
            return;
        }

        if (await db.Racks.AnyAsync(r => r.PlaceId == id))
        {
            throw new PlaceRefused($"{place.Name} already has racks, so its number stays {place.Number:00} (it is part of every rack's address).");
        }

        if (await db.Places.AnyAsync(p => p.Number == number))
        {
            throw new PlaceRefused($"Number {number:00} is already used by another place.");
        }

        var old = place.Number;
        place.Number = number;
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Place {place.Name} renumbered: {old:00} → {number:00}");
    }

    public async Task DeletePlaceAsync(int id)
    {
        if (id == Places.StartingId)
        {
            throw new PlaceRefused("The first place (where all stock started) cannot be deleted.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var place = await db.Places.FindAsync(id) ?? throw new PlaceRefused("This place is no longer there.");
        if (await db.Racks.AnyAsync(r => r.PlaceId == id))
        {
            throw new PlaceRefused($"{place.Name} still has racks. Delete its racks first.");
        }

        if (await db.Purchases.AnyAsync(p => p.PlaceId == id) || await db.Sales.AnyAsync(s => s.PlaceId == id)
            || await db.StockChangeLines.AnyAsync(l => l.FromPlaceId == id || l.ToPlaceId == id))
        {
            throw new PlaceRefused($"{place.Name} holds stock entries, so it cannot be deleted.");
        }

        db.Places.Remove(place);
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"Place deleted: {place.Number:00} {place.Name}");
    }

    // ----- Where purchases arrive and sales leave from (batch 3-4) -----

    /// <summary>The places purchases arrive in and sales leave from (repaired goods go back there too).</summary>
    public async Task<(int Purchases, int Sales)> FlowAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return (await PlaceSettings.PurchasePlaceAsync(db), await PlaceSettings.SalesPlaceAsync(db));
    }

    /// <summary>Hiten, 26 Nov 2026: purchases arrive in the godown. Changes only new purchases; saved ones keep their place.</summary>
    public Task SetPurchasePlaceAsync(int placeId) => SetFlowAsync(PlaceSettings.PurchasesKey, placeId, "Purchases arrive in");

    /// <summary>Hiten, 26 Nov 2026: sales leave from the shop; sales returns and repaired goods come back there.</summary>
    public Task SetSalesPlaceAsync(int placeId) => SetFlowAsync(PlaceSettings.SalesKey, placeId, "Sales leave from");

    private async Task SetFlowAsync(string key, int placeId, string what)
    {
        await using var db = await factory.CreateDbContextAsync();
        var place = await db.Places.FindAsync(placeId) ?? throw new PlaceRefused("This place is no longer there.");
        var setting = await db.AppSettings.FindAsync(key);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSettingEntity { Key = key, Value = placeId.ToString() });
        }
        else if (setting.Value == placeId.ToString())
        {
            return;
        }
        else
        {
            setting.Value = placeId.ToString();
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"{what}: {place.Number:00} {place.Name}");
    }

    // ----- Changing the place of a saved entry (Hiten, 26 Nov 2026: "Admin can edit") -----

    public const string OnlyAdminChangesPlace = "Only the Admin can change the place of a saved entry.";

    /// <summary>
    /// The place a saved purchase's goods are counted in. Damage recorded on the purchase came out of that place, so it moves
    /// with it (repairs, write-offs and sending back do not: they have their own places).
    /// </summary>
    public async Task ChangePurchasePlaceAsync(Guid purchaseId, int placeId, UserAccess access)
    {
        RefuseUnlessAdmin(access);
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await RefuseUnknownPlaceAsync(db, placeId);
        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId) ?? throw new PlaceRefused("This purchase was not found.");
        if (purchase.PlaceId == placeId)
        {
            return;
        }

        var old = purchase.PlaceId;
        purchase.PlaceId = placeId;
        foreach (var damage in await db.StockChanges.Include(s => s.Lines).Where(s => s.PurchaseEntityId == purchaseId).ToListAsync())
        {
            MoveEntry(damage, old, placeId);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>The place a saved sale's goods left from.</summary>
    public async Task ChangeSalePlaceAsync(Guid saleId, int placeId, UserAccess access)
    {
        RefuseUnlessAdmin(access);
        await using var db = await factory.CreateDbContextAsync();
        await RefuseUnknownPlaceAsync(db, placeId);
        var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == saleId) ?? throw new PlaceRefused("This sale was not found.");
        if (sale.PlaceId == placeId)
        {
            return;
        }

        sale.PlaceId = placeId;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The place of a saved stock change entry (the Stock changes screen) or purchase return: the entry and every line of it
    /// that took or gave pieces in the old place move to the new one.
    /// </summary>
    public async Task ChangeEntryPlaceAsync(Guid entryId, int placeId, UserAccess access)
    {
        RefuseUnlessAdmin(access);
        await using var db = await factory.CreateDbContextAsync();
        await RefuseUnknownPlaceAsync(db, placeId);
        var entry = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == entryId) ?? throw new PlaceRefused("This entry was not found.");
        var old = entry.PlaceId ?? Places.StartingId;
        if (entry.PlaceId == placeId)
        {
            return;
        }

        MoveEntry(entry, old, placeId);
        await db.SaveChangesAsync();
    }

    /// <summary>The entry's place, and its lines' sellable side where it was the old place.</summary>
    private static void MoveEntry(StockChangeEntity entry, int old, int now)
    {
        entry.PlaceId = now;
        foreach (var line in entry.Lines)
        {
            if (line.From == StockState.Sellable && (line.FromPlaceId ?? Places.StartingId) == old)
            {
                line.FromPlaceId = now;
            }

            if (line.To == StockState.Sellable && (line.ToPlaceId ?? Places.StartingId) == old)
            {
                line.ToPlaceId = now;
            }
        }
    }

    private static void RefuseUnlessAdmin(UserAccess access)
    {
        if (!access.HasAdminRole)
        {
            throw new PlaceRefused(OnlyAdminChangesPlace);
        }
    }

    private static async Task RefuseUnknownPlaceAsync(AppDbContext db, int placeId)
    {
        if (!await db.Places.AnyAsync(p => p.Id == placeId))
        {
            throw new PlaceRefused("This place is no longer there.");
        }
    }

    // ----- Racks -----

    /// <summary>Adds racks <paramref name="from"/> to <paramref name="to"/> with levels A up to <paramref name="levels"/>; racks already there are left as they are. Returns how many were added.</summary>
    public async Task<int> AddRacksAsync(int placeId, int from, int to, int levels)
    {
        if (from is < 1 or > Places.MaxNumber || to is < 1 or > Places.MaxNumber)
        {
            throw new PlaceRefused($"Rack numbers are 1 to {Places.MaxNumber}.");
        }

        if (to < from)
        {
            throw new PlaceRefused("The last rack number cannot be smaller than the first.");
        }

        RefuseLevels(levels);
        await using var db = await factory.CreateDbContextAsync();
        var place = await db.Places.FindAsync(placeId) ?? throw new PlaceRefused("This place is no longer there.");
        var existing = await db.Racks.Where(r => r.PlaceId == placeId).Select(r => r.Number).ToListAsync();
        var added = Enumerable.Range(from, to - from + 1).Where(n => !existing.Contains(n)).ToList();
        if (added.Count == 0)
        {
            throw new PlaceRefused(from == to ? $"Rack {from:00} is already there." : $"Racks {from:00} to {to:00} are already there.");
        }

        db.Racks.AddRange(added.Select(n => new RackEntity { PlaceId = placeId, Number = n, Levels = levels }));
        await db.SaveChangesAsync();
        var which = added.Count == 1 ? $"rack {added[0]:00}" : $"{added.Count} racks ({added[0]:00} to {added[^1]:00})";
        await activity.WriteAsync("Setting", $"{place.Name}: {which} added, levels A–{RackAddress.LevelLetter(levels)}");
        return added.Count;
    }

    /// <summary>Changes how many levels a rack has; a level that still holds items cannot be taken away.</summary>
    public async Task SetRackLevelsAsync(int rackId, int levels)
    {
        RefuseLevels(levels);
        await using var db = await factory.CreateDbContextAsync();
        var rack = await db.Racks.Include(r => r.Place).FirstOrDefaultAsync(r => r.Id == rackId) ?? throw new PlaceRefused("This rack is no longer there.");
        if (rack.Levels == levels)
        {
            return;
        }

        var highest = RackAddress.LevelLetter(levels);
        var above = await db.VariantRacks.Where(v => v.RackId == rackId).Select(v => v.Level).ToListAsync();
        var inUse = above.Where(l => l[0] > highest).Distinct().OrderBy(l => l).ToList();
        if (inUse.Count > 0)
        {
            throw new PlaceRefused($"Level {string.Join(", ", inUse)} of rack {rack.Place!.Number:00}-{rack.Number:00} still holds items, so it cannot be taken away.");
        }

        var old = rack.Levels;
        rack.Levels = levels;
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"{rack.Place!.Name}: rack {rack.Number:00} levels A–{RackAddress.LevelLetter(old)} → A–{highest}");
    }

    public async Task DeleteRackAsync(int rackId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rack = await db.Racks.Include(r => r.Place).FirstOrDefaultAsync(r => r.Id == rackId) ?? throw new PlaceRefused("This rack is no longer there.");
        var items = await db.VariantRacks.Where(v => v.RackId == rackId).Select(v => v.VariantEntityId).Distinct().CountAsync();
        if (items > 0)
        {
            throw new PlaceRefused($"Rack {rack.Place!.Number:00}-{rack.Number:00} still holds {items} item{(items == 1 ? "" : "s")}. Move them to another rack first.");
        }

        db.Racks.Remove(rack);
        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", $"{rack.Place!.Name}: rack {rack.Number:00} deleted");
    }

    // ----- Addresses -----

    /// <summary>
    /// The rack and level an address points to among <paramref name="places"/>, or a plain reason why not
    /// ("There is no place 05.", "Godown has no rack 31.", "Rack 02-03 has levels A–D.").
    /// </summary>
    public static (RackItem? Rack, string? Problem) Resolve(IReadOnlyList<PlaceItem> places, RackAddress address)
    {
        var place = places.FirstOrDefault(p => p.Number == address.Place);
        if (place is null)
        {
            return (null, $"There is no place {address.Place:00}.");
        }

        var rack = place.Racks.FirstOrDefault(r => r.Number == address.Rack);
        if (rack is null)
        {
            return (null, $"{place.Name} has no rack {address.Rack:00}.");
        }

        if (RackAddress.LevelNumber(address.Level) > rack.Levels)
        {
            return (null, $"Rack {address.Place:00}-{address.Rack:00} has levels {rack.LevelsText}.");
        }

        return (rack, null);
    }

    // ----- Where each variant is kept -----

    public async Task<Dictionary<Guid, List<VariantRackItem>>> RacksOfVariantsAsync(IEnumerable<Guid> variantIds)
    {
        var ids = variantIds.Distinct().ToList();
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.VariantRacks.AsNoTracking()
            .Where(v => ids.Contains(v.VariantEntityId))
            .Select(v => new { v.Id, v.VariantEntityId, v.RackId, v.Level, v.IsMain, RackNumber = v.Rack!.Number, v.Rack.PlaceId, PlaceNumber = v.Rack.Place!.Number, PlaceName = v.Rack.Place.Name })
            .ToListAsync();
        return rows
            .Select(r => new VariantRackItem(r.Id, r.VariantEntityId, r.RackId, r.PlaceId, r.PlaceName, new RackAddress(r.PlaceNumber, r.RackNumber, r.Level[0]), r.IsMain))
            .GroupBy(r => r.VariantId)
            .ToDictionary(g => g.Key, g => Ordered(g).ToList());
    }

    /// <summary>Place by place; in each place the main rack first, then by address.</summary>
    public static IEnumerable<VariantRackItem> Ordered(IEnumerable<VariantRackItem> racks) =>
        racks.OrderBy(r => r.Address.Place).ThenByDescending(r => r.IsMain).ThenBy(r => r.Address.Rack).ThenBy(r => r.Address.Level);

    /// <summary>
    /// Adds a rack level to a variant. The first one in a place becomes that place's main rack; <paramref name="makeMain"/>
    /// makes this one the main rack instead of the one before.
    /// </summary>
    public async Task AddVariantRackAsync(Guid variantId, int rackId, char level, bool makeMain)
    {
        level = char.ToUpperInvariant(level);
        await using var db = await factory.CreateDbContextAsync();
        var rack = await db.Racks.Include(r => r.Place).FirstOrDefaultAsync(r => r.Id == rackId) ?? throw new PlaceRefused("This rack is no longer there.");
        if (RackAddress.LevelNumber(level) is var n && (n < 1 || n > rack.Levels))
        {
            throw new PlaceRefused($"Rack {rack.Place!.Number:00}-{rack.Number:00} has levels A–{RackAddress.LevelLetter(rack.Levels)}.");
        }

        var variant = await VariantAsync(db, variantId);
        var address = new RackAddress(rack.Place!.Number, rack.Number, level);
        var text = level.ToString();
        if (await db.VariantRacks.AnyAsync(v => v.VariantEntityId == variantId && v.RackId == rackId && v.Level == text))
        {
            throw new PlaceRefused($"Variant {variant.VariantId} is already on {address}.");
        }

        var samePlace = await db.VariantRacks.Where(v => v.VariantEntityId == variantId && v.Rack!.PlaceId == rack.PlaceId).ToListAsync();
        var main = makeMain || samePlace.Count == 0;
        if (main)
        {
            samePlace.ForEach(v => v.IsMain = false);
        }

        db.VariantRacks.Add(new VariantRackEntity { VariantEntityId = variantId, RackId = rackId, Level = text, IsMain = main });
        Log(db, variant, $"Variant {variant.VariantId}: rack {address} added" + (main ? $" (main rack in {rack.Place.Name})" : ""));
        await db.SaveChangesAsync();
    }

    /// <summary>Makes one of a variant's racks the main rack of its place.</summary>
    public async Task SetMainAsync(int variantRackId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await RowAsync(db, variantRackId);
        if (row.IsMain)
        {
            return;
        }

        var samePlace = await db.VariantRacks.Where(v => v.VariantEntityId == row.VariantEntityId && v.Rack!.PlaceId == row.Rack!.PlaceId).ToListAsync();
        samePlace.ForEach(v => v.IsMain = v.Id == row.Id);
        var variant = await VariantAsync(db, row.VariantEntityId);
        Log(db, variant, $"Variant {variant.VariantId}: main rack in {row.Rack!.Place!.Name} is now {AddressOf(row)}");
        await db.SaveChangesAsync();
    }

    /// <summary>Takes a rack off a variant. When it was the main rack, the next rack of that place becomes the main one.</summary>
    public async Task RemoveVariantRackAsync(int variantRackId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await RowAsync(db, variantRackId);
        var variant = await VariantAsync(db, row.VariantEntityId);
        db.VariantRacks.Remove(row);
        var text = $"Variant {variant.VariantId}: rack {AddressOf(row)} removed";
        if (row.IsMain)
        {
            var next = await db.VariantRacks.Include(v => v.Rack).ThenInclude(r => r!.Place)
                .Where(v => v.VariantEntityId == row.VariantEntityId && v.Id != row.Id && v.Rack!.PlaceId == row.Rack!.PlaceId)
                .OrderBy(v => v.Rack!.Number).ThenBy(v => v.Level).FirstOrDefaultAsync();
            if (next is not null)
            {
                next.IsMain = true;
                text += $"; main rack in {row.Rack!.Place!.Name} is now {AddressOf(next)}";
            }
        }

        Log(db, variant, text);
        await db.SaveChangesAsync();
    }

    /// <summary>"Same for all variants": every other variant of the product gets exactly this variant's racks. Returns how many variants were changed.</summary>
    public async Task<int> CopyToOtherVariantsAsync(Guid variantId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var variant = await VariantAsync(db, variantId);
        var source = await db.VariantRacks.Include(v => v.Rack).ThenInclude(r => r!.Place).Where(v => v.VariantEntityId == variantId).ToListAsync();
        var others = await db.Variants.Where(v => v.ProductEntityId == variant.ProductEntityId && v.Id != variantId).Select(v => v.Id).ToListAsync();
        if (others.Count == 0)
        {
            throw new PlaceRefused("This product has no other variants.");
        }

        var otherIds = others.ToHashSet();
        db.VariantRacks.RemoveRange(await db.VariantRacks.Where(v => otherIds.Contains(v.VariantEntityId)).ToListAsync());
        foreach (var other in others)
        {
            db.VariantRacks.AddRange(source.Select(s => new VariantRackEntity { VariantEntityId = other, RackId = s.RackId, Level = s.Level, IsMain = s.IsMain }));
        }

        var list = source.Count == 0 ? "no racks" : string.Join(", ", source.OrderBy(s => s.Rack!.Place!.Number).ThenByDescending(s => s.IsMain).ThenBy(s => s.Rack!.Number).ThenBy(s => s.Level)
            .Select(s => AddressOf(s) + (s.IsMain ? " (main)" : "")));
        Log(db, variant, $"Racks of variant {variant.VariantId} copied to all {others.Count} other variant{(others.Count == 1 ? "" : "s")}: {list}");
        await db.SaveChangesAsync();
        return others.Count;
    }

    // ----- Helpers -----

    private static string? Clean(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static void RefuseLevels(int levels)
    {
        if (levels is < 1 or > Places.MaxLevels)
        {
            throw new PlaceRefused($"A rack has 1 to {Places.MaxLevels} levels (A to Z).");
        }
    }

    private static async Task RefuseNameTakenAsync(AppDbContext db, string name, int? exceptId)
    {
        var lower = name.ToLower();
        if (await db.Places.AnyAsync(p => p.Id != exceptId && p.Name.ToLower() == lower))
        {
            throw new PlaceRefused($"There is already a place called \"{name}\".");
        }
    }

    private static async Task<VariantEntity> VariantAsync(AppDbContext db, Guid variantId) =>
        await db.Variants.Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == variantId)
        ?? throw new PlaceRefused("This variant is no longer there.");

    private static async Task<VariantRackEntity> RowAsync(AppDbContext db, int variantRackId) =>
        await db.VariantRacks.Include(v => v.Rack).ThenInclude(r => r!.Place).FirstOrDefaultAsync(v => v.Id == variantRackId)
        ?? throw new PlaceRefused("This rack was already taken off.");

    private static RackAddress AddressOf(VariantRackEntity row) => new(row.Rack!.Place!.Number, row.Rack.Number, row.Level[0]);

    /// <summary>One line in the product's history, saved with the change itself.</summary>
    private void Log(AppDbContext db, VariantEntity variant, string text) => db.ActivityLog.Add(new ActivityEntry
    {
        At = DateTime.Now, By = currentUser.Name, SaveId = Guid.NewGuid(), Action = "Changed", Text = text,
        RecordType = nameof(ProductEntity), RecordId = variant.ProductEntityId?.ToString(),
        RecordLabel = $"Product {variant.Product?.Name} ({variant.Product?.ProductId})",
    });
}
