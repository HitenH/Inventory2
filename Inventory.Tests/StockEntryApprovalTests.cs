using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 2, batches 2-7 and 2-8: the Stock changes screen (damage found in the shop, count corrections, write-offs, opening
/// stock), the reasons list, and approval (the Admin's switch, waiting entries, approve / reject).
/// </summary>
public class StockEntryApprovalTests
{
    private static readonly DateOnly Day = new(2030, 5, 6);

    private static UserAccess Admin() => new("Admin", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now);
    private static UserAccess Approver() => new("Checker", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.StockApprove }, null)], DateTime.Now);
    private static UserAccess Clerk() => new("Clerk", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.ScreenStockChanges }, null)], DateTime.Now);

    private static StockEntryService Entries(TestDatabase db) => new(db.Factory, new ActivityLogService(db.Factory, db.CurrentUser));

    private static async Task StockUpAsync(TestDatabase db, Seed seed, int pieces = 20)
    {
        await using var c = db.Open();
        c.Purchases.Add(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1,
            PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = pieces }] });
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<StockFigures> StockOf(TestDatabase db, Guid variant) =>
        (await new ProductRepositoryEF(db.Factory).GetStockByVariantIdsAsync([variant]))[variant];

    // ---------------------------------------------------------------- 2-7: the Stock changes screen

    [Fact]
    public async Task Damage_found_in_the_shop_moves_pieces_to_damaged_and_shows_on_the_damage_report()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await StockUpAsync(db, seed);
        var entries = Entries(db);

        var entry = await entries.CreateAsync(StockEntryType.DamagedInShop, Day, "Damaged in shop", " fell off the shelf ");
        await entries.AddLinesAsync(entry.Id, [(seed.ProductId, seed.Variant1Id, 3, 1, "top rack")]);
        Assert.Equal(new StockFigures(17, 3, 0), await StockOf(db, seed.Variant1Id));

        var loaded = (await entries.GetAsync(entry.Id))!;
        Assert.Equal((StockChangeKind.Damage, StockChangeStatus.Approved, "Damaged in shop", "fell off the shelf"), (loaded.Kind, loaded.Status, loaded.Reason, loaded.Remarks));
        var line = loaded.Lines.Single();
        Assert.Equal((StockState.Sellable, StockState.Damaged, "Damaged in shop", "top rack"), (line.From, line.To, line.Reason, line.Note));

        var lot = Assert.Single(await new DamagedGoodsService(db.Factory).LotsAsync());
        Assert.Equal((line.Id, 3, $"/stockchange/{entry.Id}?from=damage"), (lot.Key, lot.Pieces, lot.FromLink));
        Assert.StartsWith("Damaged in the shop, entry 1 of 06-05-2030", lot.From);

        var listed = Assert.Single(await entries.EntriesAsync());
        Assert.Equal((StockEntryType.DamagedInShop, 3, 1), (listed.Type, listed.Pieces, listed.Lines));
    }

    [Fact]
    public async Task A_count_correction_takes_found_more_and_found_fewer()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await StockUpAsync(db, seed);
        var entries = Entries(db);

        var entry = await entries.CreateAsync(StockEntryType.CountCorrection, Day, "Count difference", null);
        await entries.AddLinesAsync(entry.Id, [(seed.ProductId, seed.Variant1Id, -2, 1, null), (seed.ProductId, seed.Variant2Id, 5, 2, null)]);
        Assert.Equal(new StockFigures(18, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(5, 0, 0), await StockOf(db, seed.Variant2Id));

        var loaded = (await entries.GetAsync(entry.Id))!;
        var lines = new Inventory.Shared.StockEntryLines(loaded, StockEntryType.CountCorrection, entries).Load();
        Assert.Equal([-2, 5], lines.Select(l => l.Quantity));   // shown the way they were typed

        var fewer = loaded.Lines.Single(l => l.VariantEntityId == seed.Variant1Id);
        await entries.UpdateLineAsync(fewer.Id, seed.ProductId, seed.Variant1Id, 4, 1, null);   // it was 4 more after all
        Assert.Equal(new StockFigures(24, 0, 0), await StockOf(db, seed.Variant1Id));

        Assert.Contains("the count found", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.AddLinesAsync(entry.Id, [(seed.ProductId, seed.Variant1Id, 0, 3, null)]))).Message);
    }

    [Fact]
    public async Task Written_off_and_opening_stock_move_good_stock_out_and_in()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await StockUpAsync(db, seed);
        var entries = Entries(db);

        var writeOff = await entries.CreateAsync(StockEntryType.WrittenOff, Day, "Other", null);
        await entries.AddLinesAsync(writeOff.Id, [(seed.ProductId, seed.Variant1Id, 4, 1, null)]);
        var opening = await entries.CreateAsync(StockEntryType.OpeningStock, Day, "Other", null);
        await entries.AddLinesAsync(opening.Id, [(seed.ProductId, seed.Variant2Id, 9, 1, null)]);
        Assert.Equal(new StockFigures(16, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(9, 0, 0), await StockOf(db, seed.Variant2Id));
        Assert.Equal("The quantity must be more than 0.", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.AddLinesAsync(opening.Id, [(seed.ProductId, seed.Variant2Id, -1, 2, null)]))).Message);

        Assert.Equal(2, (await entries.EntriesAsync()).Count);
        await entries.DeleteLineAsync((await entries.GetAsync(writeOff.Id))!.Lines.Single().Id);
        await entries.DeleteAsync(writeOff.Id);
        Assert.Equal(new StockFigures(20, 0, 0), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Changing_the_reason_changes_it_on_every_line()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var entries = Entries(db);
        var entry = await entries.CreateAsync(StockEntryType.DamagedInShop, Day, "Damaged in shop", null);
        await entries.AddLinesAsync(entry.Id, [(seed.ProductId, seed.Variant1Id, 1, 1, null), (seed.ProductId, seed.Variant2Id, 1, 2, null)]);

        await entries.UpdateAsync(entry.Id, Day.AddDays(1), "Other", "checked");
        var loaded = (await entries.GetAsync(entry.Id))!;
        Assert.Equal(("Other", "checked", Day.AddDays(1)), (loaded.Reason, loaded.Remarks, loaded.Date));
        Assert.All(loaded.Lines, l => Assert.Equal("Other", l.Reason));
    }

    [Fact]
    public async Task Shop_damage_already_dealt_with_on_the_damage_report_is_protected()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await StockUpAsync(db, seed);
        var entries = Entries(db);
        var entry = await entries.CreateAsync(StockEntryType.DamagedInShop, Day, "Damaged in shop", null);
        await entries.AddLinesAsync(entry.Id, [(seed.ProductId, seed.Variant1Id, 3, 1, null)]);
        var line = (await entries.GetAsync(entry.Id))!.Lines.Single();
        await new DamagedGoodsService(db.Factory).RepairAsync([new LotPieces(line.Id, 2)]);

        Assert.Contains("cannot be less than 2", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.UpdateLineAsync(line.Id, seed.ProductId, seed.Variant1Id, 1, 1, null))).Message);
        Assert.Contains("another item", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.UpdateLineAsync(line.Id, seed.ProductId, seed.Variant2Id, 3, 1, null))).Message);
        await Assert.ThrowsAsync<StockChangeRefused>(() => entries.DeleteLineAsync(line.Id));
        await Assert.ThrowsAsync<StockChangeRefused>(() => entries.DeleteAsync(entry.Id));
        await entries.UpdateLineAsync(line.Id, seed.ProductId, seed.Variant1Id, 2, 1, null);   // down to the 2 repaired is fine
        Assert.Equal(new StockFigures(20, 0, 0), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Entries_made_elsewhere_are_not_on_the_Stock_changes_screen()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var returns = new StockChangeService(db.Factory);
        var ret = await returns.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        Assert.Empty(await Entries(db).EntriesAsync());
        Assert.Null(await Entries(db).GetAsync(ret.Id));
    }

    // ---------------------------------------------------------------- 2-7: the reasons list

    [Fact]
    public async Task The_reasons_list_can_be_added_to_renamed_reordered_and_shortened()
    {
        using var db = new TestDatabase();
        var entries = Entries(db);
        var returns = new StockChangeService(db.Factory);

        await entries.AddReasonAsync("  Rat damage ");
        Assert.Contains("already on the list", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.AddReasonAsync("rat DAMAGE"))).Message);
        var all = await entries.AllReasonsAsync();
        Assert.Equal("Rat damage", all.Last().Name);

        await entries.RenameReasonAsync(all.Last().Id, "Rodents");
        await Assert.ThrowsAsync<StockChangeRefused>(() => entries.RenameReasonAsync(all.Last().Id, "Other"));
        await entries.MoveReasonAsync(all.Last().Id, -1);
        await entries.SetReasonActiveAsync(all.First().Id, false);

        var offered = await returns.ReasonsAsync();
        Assert.DoesNotContain(all.First().Name, offered);   // taken off: no longer offered
        Assert.Equal("Rodents", offered[^2]);              // moved up one
        Assert.Equal("Other", offered[^1]);

        var log = (await db.Open().ActivityLog.AsNoTracking().Where(a => a.Action == "Setting").Select(a => a.Text).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Stock change reason added: Rat damage", log);
        Assert.Contains("Stock change reason renamed: Rat damage → Rodents", log);
    }

    [Fact]
    public async Task The_last_reason_cannot_be_taken_off()
    {
        using var db = new TestDatabase();
        var entries = Entries(db);
        var all = await entries.AllReasonsAsync();
        foreach (var r in all.Skip(1))
        {
            await entries.SetReasonActiveAsync(r.Id, false);
        }

        Assert.Contains("At least one reason", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.SetReasonActiveAsync(all[0].Id, false))).Message);
    }

    // ---------------------------------------------------------------- 2-8: approval

    [Fact]
    public async Task Approval_is_off_until_the_Admin_switches_it_on_and_only_the_Admin_may()
    {
        using var db = new TestDatabase();
        var entries = Entries(db);
        Assert.False(await entries.IsApprovalOnAsync());

        await Assert.ThrowsAsync<StockChangeRefused>(() => entries.SetApprovalAsync(true, Approver()));
        await entries.SetApprovalAsync(true, Admin());
        Assert.True(await entries.IsApprovalOnAsync());
        await entries.SetApprovalAsync(false, Admin());
        Assert.False(await entries.IsApprovalOnAsync());

        var log = await db.Open().ActivityLog.AsNoTracking().Where(a => a.Action == "Setting").OrderBy(a => a.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Stock changes need approval: switched on", "Stock changes need approval: switched off"], log.Select(a => a.Text));
        Assert.All(log, a => Assert.Equal("Admin", a.By));
    }

    [Fact]
    public async Task With_approval_on_a_stock_change_waits_and_moves_stock_only_when_approved()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await StockUpAsync(db, seed);
        var entries = Entries(db);
        await entries.SetApprovalAsync(true, Admin());

        var entry = await entries.CreateAsync(StockEntryType.WrittenOff, Day, "Other", null);
        await entries.AddLinesAsync(entry.Id, [(seed.ProductId, seed.Variant1Id, 4, 1, null)]);
        Assert.Equal(StockChangeStatus.Waiting, (await entries.GetAsync(entry.Id))!.Status);
        Assert.Equal(new StockFigures(20, 0, 4), await StockOf(db, seed.Variant1Id));   // not moved; shown as waiting

        var waiting = Assert.Single(await entries.WaitingAsync());
        Assert.StartsWith("Write-off 1 of 06-05-2030", waiting.Label);
        Assert.Equal(["P1 Product One / V1: 4 out of stock"], waiting.Lines);
        Assert.Equal($"/stockchange/{entry.Id}", waiting.Link);

        await Assert.ThrowsAsync<StockChangeRefused>(() => entries.DecideAsync(entry.Id, true, Clerk()));   // needs the switch
        await entries.DecideAsync(entry.Id, true, Approver());
        Assert.Equal(new StockFigures(16, 0, 0), await StockOf(db, seed.Variant1Id));
        var decided = (await entries.GetAsync(entry.Id))!;
        Assert.Equal((StockChangeStatus.Approved, "Checker"), (decided.Status, decided.DecidedBy));
        Assert.Contains("already approved by Checker", (await Assert.ThrowsAsync<StockChangeRefused>(() => entries.DecideAsync(entry.Id, false, Approver()))).Message);

        // Changing a line of an approved entry sends it for approval again: its stock waits again.
        await entries.UpdateLineAsync(decided.Lines.Single().Id, seed.ProductId, seed.Variant1Id, 5, 1, null);
        Assert.Equal(StockChangeStatus.Waiting, (await entries.GetAsync(entry.Id))!.Status);
        Assert.Equal(new StockFigures(20, 0, 5), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task A_rejected_stock_change_never_moves_stock()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var entries = Entries(db);
        await entries.SetApprovalAsync(true, Admin());
        var returns = new StockChangeService(db.Factory);
        var ret = await returns.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await returns.AddSalesReturnLineAsync(ret.Id, seed.ProductId, seed.Variant1Id, 3, 1);
        Assert.Equal(new StockFigures(0, 0, 3), await StockOf(db, seed.Variant1Id));

        await entries.DecideAsync(ret.Id, false, Approver());
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Empty(await entries.WaitingAsync());
        Assert.Equal(StockChangeStatus.Rejected, (await returns.GetSalesReturnAsync(ret.Id))!.Status);
    }

    [Fact]
    public async Task With_approval_off_again_new_stock_changes_count_at_once_but_old_waiting_ones_still_wait()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var entries = Entries(db);
        await entries.SetApprovalAsync(true, Admin());
        var waitingOne = await entries.CreateAsync(StockEntryType.OpeningStock, Day, "Other", null);
        await entries.AddLinesAsync(waitingOne.Id, [(seed.ProductId, seed.Variant1Id, 2, 1, null)]);
        await entries.SetApprovalAsync(false, Admin());

        var now = await entries.CreateAsync(StockEntryType.OpeningStock, Day, "Other", null);
        await entries.AddLinesAsync(now.Id, [(seed.ProductId, seed.Variant1Id, 5, 1, null)]);
        Assert.Equal(new StockFigures(5, 0, 2), await StockOf(db, seed.Variant1Id));
        Assert.Equal(1, await entries.WaitingCountAsync());
    }

    [Fact]
    public async Task Damage_and_the_damage_report_wait_for_approval_too()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var purchase = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 3 });
        await new PurchaseVariantRepositoryEF(db.Factory).Create(new PurchaseVariant { PurchaseEntityId = purchase, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 10 });
        var line = (await new StockChangeService(db.Factory).DamageCandidatesAsync(DamageSource.Purchase, purchase)).Single().LineId;
        var entries = Entries(db);
        var damaged = new DamagedGoodsService(db.Factory);
        await entries.SetApprovalAsync(true, Admin());

        var result = await new StockChangeService(db.Factory).RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line, 4, null)], null);
        Assert.True(result.Waiting);
        Assert.EndsWith("Waiting for approval.", result.Text);
        Assert.Equal(new StockFigures(10, 0, 4), await StockOf(db, seed.Variant1Id));
        var pending = Assert.Single(await damaged.LotsAsync());
        Assert.Equal((0, 4), (pending.Pieces, pending.Waiting));   // shown, but nothing to act on until approved

        var damage = Assert.Single(await entries.WaitingAsync());
        Assert.Equal("Purchase 3 of 06-05-2030, Supplier One", damage.Party);
        Assert.Equal($"/purchase/{purchase}", damage.Link);
        await entries.DecideAsync(damage.Id, true, Approver());
        Assert.Equal(new StockFigures(6, 4, 0), await StockOf(db, seed.Variant1Id));

        var repair = await damaged.RepairAsync([new LotPieces(line, 1)]);
        Assert.Equal(StockChangeStatus.Waiting, repair.Status);
        var lot = Assert.Single(await damaged.LotsAsync());
        Assert.Equal((3, 1), (lot.Pieces, lot.Waiting));   // the waiting repair is kept aside: it cannot be taken twice
        await Assert.ThrowsAsync<StockChangeRefused>(() => damaged.WriteOffAsync([new LotPieces(line, 4)]));
        await entries.DecideAsync(repair.Id, true, Approver());
        Assert.Equal(new StockFigures(7, 3, 0), await StockOf(db, seed.Variant1Id));
    }

    // ---------------------------------------------------------------- screens

    [Fact]
    public void The_new_screens_have_their_switches_and_keys()
    {
        Assert.Equal(Permissions.ScreenStockChanges, Permissions.ForPath("/stockchanges"));
        Assert.Equal(Permissions.ScreenStockChanges, Permissions.ForPath("/stockchange/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Equal(Permissions.StockApprove, Permissions.ForPath("/approvals"));
        Assert.Equal(Permissions.AdminSettings, Permissions.ForPath("/reasons"));
        Assert.Contains(Permissions.All, p => p.Key == Permissions.StockApprove && p.Label == "Approve stock changes");
        Assert.Equal("/stockchanges", Shortcuts.Back("/stockchange/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Equal("/damage", Shortcuts.Back("/stockchange/2f1c2d3e-0000-0000-0000-000000000000?from=damage"));
        Assert.Equal("/stockchange", Shortcuts.Target("/stockchanges", 'N', false));
        Assert.Equal("/settings", Shortcuts.Back("/reasons"));
    }
}
