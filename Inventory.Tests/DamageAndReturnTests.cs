using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 2, batches 2-3 and 2-4: "Record damage" on a purchase or a sales return (one pop-up, all lines at once), and the
/// sales return itself. Rules in Service/StockChangeService.cs.
/// </summary>
public class DamageAndReturnTests
{
    private static readonly DateOnly Day = new(2030, 3, 5);

    /// <summary>A purchase from the seed supplier: V1 × 10 and V2 × 4, through the real repositories.</summary>
    private static async Task<(Guid Purchase, Guid Line1, Guid Line2)> PurchaseAsync(TestDatabase db, Seed seed)
    {
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lines = new PurchaseVariantRepositoryEF(db.Factory);
        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 12 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 10 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, SerialNumber = 2, Quantity = 4 });
        await using var c = db.Open();
        var ids = c.PurchaseVariant.Where(l => l.PurchaseEntityId == id).OrderBy(l => l.SerialNumber).Select(l => l.Id).ToList();
        return (id, ids[0], ids[1]);
    }

    private static async Task<StockFigures> StockOf(TestDatabase db, Guid variant) =>
        (await new ProductRepositoryEF(db.Factory).GetStockByVariantIdsAsync([variant]))[variant];

    // ---------------------------------------------------------------- 2-3: damage on a purchase

    [Fact]
    public async Task The_pop_up_lists_every_line_of_the_purchase_with_what_is_damaged_now()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, _) = await PurchaseAsync(db, seed);
        var service = new StockChangeService(db.Factory);

        var before = await service.DamageCandidatesAsync(DamageSource.Purchase, purchase);
        Assert.Equal([("V1", 10, 0), ("V2", 4, 0)], before.Select(c => (c.VariantId, c.Received, c.Damaged)));
        Assert.Equal("P1 Product One / V1", before[0].Label);

        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 3, "Broken in transit")], null);
        var after = await service.DamageCandidatesAsync(DamageSource.Purchase, purchase);
        Assert.Equal((3, 0), (after[0].Damaged, after[0].Lowest));
    }

    [Fact]
    public async Task Recording_damage_makes_one_stock_change_linked_to_the_purchase_and_moves_pieces_to_damaged()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, line2) = await PurchaseAsync(db, seed);
        var service = new StockChangeService(db.Factory);

        // The pop-up sends every line: changed ones with their new number, the rest unchanged (0 or blank).
        var result = await service.RecordDamageAsync(DamageSource.Purchase, purchase,
            [new DamageEntry(line1, 2, "Broken in transit"), new DamageEntry(line2, 0, "Other")], "  box crushed ");
        Assert.Equal(new DamageResult(2, 0), result);
        Assert.Equal("Damage recorded: 2 more damaged.", result.Text);

        Assert.Equal(new StockFigures(8, 2, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(4, 0, 0), await StockOf(db, seed.Variant2Id));

        await using var c = db.Open();
        var change = c.StockChanges.Include(s => s.Lines).Single();
        Assert.Equal(StockChangeKind.Damage, change.Kind);
        Assert.Equal(StockChangeStatus.Approved, change.Status);   // approval switch off (Hiten)
        Assert.Equal(purchase, change.PurchaseEntityId);
        Assert.Equal("box crushed", change.Remarks);
        Assert.Equal(1, change.VoucherId);
        var line = Assert.Single(change.Lines);
        Assert.Equal((StockState.Sellable, StockState.Damaged, 2, "Broken in transit"), (line.From, line.To, line.Quantity, line.Reason));
        Assert.Equal(line1, line.PurchaseVariantId);
        Assert.Equal(seed.SupplierId, line.ReturnSupplierEntityId);   // goes back to the purchase's supplier unless changed

        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line2, 1, "Other")], null);
        Assert.Equal(2, c.StockChanges.AsNoTracking().Where(s => s.Date == change.Date).Max(s => s.VoucherId));   // numbered per day
    }

    [Theory]
    [InlineData(11, "only 10 pieces came in, so no more than 10 can be damaged")]
    [InlineData(-1, "must be 0 or more")]
    [InlineData(0, StockChangeRules.NothingChanged)]
    public async Task Damage_beyond_what_came_in_or_no_change_at_all_is_refused_and_nothing_is_saved(int damaged, string message)
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, _) = await PurchaseAsync(db, seed);
        var service = new StockChangeService(db.Factory);

        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, damaged, "Other")], null));
        Assert.Contains(message, ex.Message);
        await using var c = db.Open();
        Assert.Empty(c.StockChanges);
        Assert.Equal(new StockFigures(10, 0, 0), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Recorded_damage_can_be_lowered_when_pieces_are_not_damaged_after_all()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, line2) = await PurchaseAsync(db, seed);
        var service = new StockChangeService(db.Factory);
        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 3, "Broken in transit")], null);

        // Hiten, 26 Nov 2026: on a closer look only 1 is damaged; at the same time 2 of the other line are.
        var result = await service.RecordDamageAsync(DamageSource.Purchase, purchase,
            [new DamageEntry(line1, 1, "Broken in transit"), new DamageEntry(line2, 2, "Manufacturing defect")], null);
        Assert.Equal(new DamageResult(2, 2), result);
        Assert.Equal("Damage recorded: 2 more damaged, 2 not damaged after all.", result.Text);
        Assert.Equal(new StockFigures(9, 1, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(2, 2, 0), await StockOf(db, seed.Variant2Id));

        var now = await service.DamageCandidatesAsync(DamageSource.Purchase, purchase);
        Assert.Equal([1, 2], now.Select(c => c.Damaged));

        // Nothing earlier was changed: the first record stays, the correction is its own entry.
        await using var c = db.Open();
        var changes = c.StockChanges.Include(s => s.Lines).OrderBy(s => s.VoucherId).ToList();
        Assert.Equal(2, changes.Count);
        Assert.Equal(3, changes[0].Lines.Single().Quantity);
        var back = changes[1].Lines.Single(l => l.PurchaseVariantId == line1);
        Assert.Equal((StockState.Damaged, StockState.Sellable, 2, StockReasons.NotDamaged), (back.From, back.To, back.Quantity, back.Reason));

        // All the way down to 0: the line is free again (it can be lowered and deleted).
        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 0, null)], null);
        Assert.Equal(new StockFigures(10, 0, 0), await StockOf(db, seed.Variant1Id));
        await new PurchaseVariantRepositoryEF(db.Factory).Delete(new PurchaseVariant { Id = line1 });
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Damage_already_dealt_with_cannot_be_lowered_below_it()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, _) = await PurchaseAsync(db, seed);
        var service = new StockChangeService(db.Factory);
        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 3, null)], null);

        // Batches 2-5/2-6 will repair, write off or send back damaged pieces; such a line takes from the damage line.
        await using (var c = db.Open())
        {
            var damageLine = c.StockChangeLines.Single();
            c.StockChanges.Add(new StockChangeEntity
            {
                Kind = StockChangeKind.Repaired, Date = Day, VoucherId = 1, Status = StockChangeStatus.Approved,
                Lines = [new StockChangeLineEntity { SerialNumber = 1, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2, From = StockState.Damaged, To = StockState.Sellable, SourceLineId = damageLine.Id }],
            });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var line = (await service.DamageCandidatesAsync(DamageSource.Purchase, purchase))[0];
        Assert.Equal((3, 2), (line.Damaged, line.Lowest));
        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 1, null)], null));
        Assert.Contains("2 damaged pieces have already been repaired, written off or sent back, so it cannot be less than 2.", ex.Message);
        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 2, null)], null);   // down to 2 is fine
    }

    [Fact]
    public async Task A_purchase_line_with_damage_cannot_be_deleted_lowered_below_it_or_changed_to_another_item()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, _) = await PurchaseAsync(db, seed);
        await new StockChangeService(db.Factory).RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 3, null)], null);
        var lines = new PurchaseVariantRepositoryEF(db.Factory);
        PurchaseVariant Line(int qty, Guid? variant = null) => new() { Id = line1, PurchaseEntityId = purchase, ProductEntityId = seed.ProductId, VariantEntityId = variant ?? seed.Variant1Id, SerialNumber = 1, Quantity = qty };

        var deleted = await Assert.ThrowsAsync<StockChangeRefused>(() => lines.Delete(new PurchaseVariant { Id = line1 }));
        Assert.Equal(StockChangeRules.DamagedLineCannotBeDeleted, deleted.Message);
        var lower = await Assert.ThrowsAsync<StockChangeRefused>(() => lines.Update(Line(2)));
        Assert.Equal("3 pieces of this line are recorded as damaged, so the quantity cannot be less than 3.", lower.Message);
        var other = await Assert.ThrowsAsync<StockChangeRefused>(() => lines.Update(Line(10, seed.Variant2Id)));
        Assert.Contains("cannot be changed to another item", other.Message);
        var whole = await Assert.ThrowsAsync<StockChangeRefused>(() => new PurchaseRepositoryEF(db.Factory).Delete(new PurchaseEntity { Id = purchase }));
        Assert.Equal(StockChangeRules.DamagedPurchaseCannotBeDeleted, whole.Message);

        await lines.Update(Line(3));    // down to exactly the damaged pieces is fine
        await lines.Update(Line(12));   // more is fine
        Assert.Equal(new StockFigures(9, 3, 0), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task Damage_on_a_purchase_shows_in_the_purchase_history_and_as_its_own_entry()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, _) = await PurchaseAsync(db, seed);
        await using (var clean = db.Open())
        {
            clean.ActivityLog.RemoveRange(clean.ActivityLog.ToList());
            await clean.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        db.CurrentUser.Name = "Hiten";
        var service = new StockChangeService(db.Factory);
        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 2, "Broken in transit")], null);

        await using (var c = db.Open())
        {
            var log = c.ActivityLog.AsNoTracking().OrderBy(a => a.Id).ToList();
            var onPurchase = Assert.Single(log, a => a.RecordType == nameof(PurchaseEntity));
            Assert.Equal(purchase.ToString(), onPurchase.RecordId, ignoreCase: true);
            Assert.Equal("Purchase 12 of 05-03-2030", onPurchase.RecordLabel);
            Assert.StartsWith("Damage recorded (Damage 1 of ", onPurchase.Text);
            Assert.EndsWith("): P1 Product One / V1 2 damaged (Broken in transit)", onPurchase.Text);
            Assert.Equal("Hiten", onPurchase.By);
            var created = Assert.Single(log, a => a.RecordType == nameof(StockChangeEntity) && a.Action == "Created" && !a.IsLineDetail);
            Assert.Equal("Created: damage on Purchase 12 of 05-03-2030, 1 line, 2 pieces", created.Text);
            Assert.StartsWith("Damage 1 of ", created.RecordLabel);
        }

        await service.RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 1, null)], null);
        await using var after = db.Open();
        var correction = after.ActivityLog.AsNoTracking().Where(a => a.RecordType == nameof(PurchaseEntity)).OrderBy(a => a.Id).ToList().Last();
        Assert.EndsWith("): P1 Product One / V1 1 not damaged after all", correction.Text);
    }

    [Theory]
    [InlineData(0, 2, 3, 10, null)]
    [InlineData(3, 2, 2, 2, "3 pieces of this line are recorded as damaged, so the quantity cannot be less than 3.")]
    [InlineData(1, 2, 2, 1, null)]
    [InlineData(1, 2, 4, 1, "1 piece of this line is recorded as damaged, so the line cannot be changed to another item.")]
    public void The_line_change_rule(int damaged, int oldVariant, int newVariant, int quantity, string? expected)
    {
        var product = Guid.NewGuid();
        Guid V(int n) => new($"00000000-0000-0000-0000-00000000000{n}");
        Assert.Equal(expected, StockChangeRules.LineChangeProblem(damaged, product, V(oldVariant), product, V(newVariant), quantity));
    }

    // ---------------------------------------------------------------- 2-4: sales returns

    [Fact]
    public async Task A_sales_return_puts_the_pieces_back_into_sellable_stock()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);

        var first = await service.CreateSalesReturnAsync(seed.CustomerId, Day, " wrong size ");
        var second = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        Assert.Equal((1, 2), (first.VoucherId, second.VoucherId));   // numbered per day
        Assert.Equal("wrong size", first.Remarks);

        await service.AddSalesReturnLineAsync(first.Id, seed.ProductId, seed.Variant1Id, 3, 1);
        await service.AddSalesReturnLinesAsync(first.Id, [(seed.ProductId, seed.Variant2Id, 2, 2), (seed.ProductId, seed.Variant1Id, 1, 3)]);
        Assert.Equal(new StockFigures(4, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(2, 0, 0), await StockOf(db, seed.Variant2Id));

        var loaded = (await service.GetSalesReturnAsync(first.Id))!;
        Assert.Equal("Customer One", loaded.Customer!.Name);
        Assert.Equal([1, 2, 3], loaded.Lines.OrderBy(l => l.SerialNumber).Select(l => l.SerialNumber));
        Assert.All(loaded.Lines, l => Assert.Equal((StockState.Outside, StockState.Sellable), (l.From, l.To)));

        var list = await service.SalesReturnsAsync();
        Assert.Equal(6, list.Single(r => r.Id == first.Id).Pieces);
        Assert.Equal(0, list.Single(r => r.Id == second.Id).Pieces);
        Assert.Null(await service.GetSalesReturnAsync(Guid.NewGuid()));   // honest "not found"
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task A_returned_quantity_must_be_more_than_zero(int quantity)
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);

        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => service.AddSalesReturnLineAsync(ret.Id, seed.ProductId, seed.Variant1Id, quantity, 1));
        Assert.Equal("The quantity returned must be more than 0.", ex.Message);
        Assert.Equal("The quantity returned must be more than 0.", new Inventory.Shared.SalesReturnLines(ret, service).ExtraProblem(new Inventory.Models.LineEditorModel { Quantity = quantity }));
        await Assert.ThrowsAsync<StockChangeRefused>(() => service.CreateSalesReturnAsync(Guid.Empty, Day, null));   // needs a customer
    }

    [Fact]
    public async Task Sales_return_lines_can_be_changed_and_deleted_and_the_return_deleted()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await service.AddSalesReturnLineAsync(ret.Id, seed.ProductId, seed.Variant1Id, 3, 1);
        var lineId = (await service.GetSalesReturnAsync(ret.Id))!.Lines.Single().Id;

        await service.UpdateSalesReturnLineAsync(lineId, seed.ProductId, seed.Variant2Id, 5, 1);
        Assert.Equal(new StockFigures(5, 0, 0), await StockOf(db, seed.Variant2Id));
        await service.UpdateSalesReturnAsync(ret.Id, seed.CustomerId, Day.AddDays(1), "changed");
        Assert.Equal((Day.AddDays(1), "changed", 1), ((await service.GetSalesReturnAsync(ret.Id))!.Date, (await service.GetSalesReturnAsync(ret.Id))!.Remarks, (await service.GetSalesReturnAsync(ret.Id))!.VoucherId));

        await service.DeleteSalesReturnLineAsync(lineId);
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant2Id));
        await service.DeleteSalesReturnAsync(ret.Id);
        await using var c = db.Open();
        Assert.Empty(c.StockChanges);
    }

    [Fact]
    public async Task Damage_found_on_a_sales_return_is_recorded_with_the_same_pop_up_and_then_guards_the_return()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await service.AddSalesReturnLineAsync(ret.Id, seed.ProductId, seed.Variant1Id, 4, 1);

        var candidates = await service.DamageCandidatesAsync(DamageSource.SalesReturn, ret.Id);
        var line = Assert.Single(candidates);
        Assert.Equal((4, 0), (line.Received, line.Damaged));

        await service.RecordDamageAsync(DamageSource.SalesReturn, ret.Id, [new DamageEntry(line.LineId, 1, "Damaged in shop")], null);
        Assert.Equal(new StockFigures(3, 1, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(1, (await service.DamageCandidatesAsync(DamageSource.SalesReturn, ret.Id)).Single().Damaged);

        await using (var c = db.Open())
        {
            var damage = c.StockChanges.Include(s => s.Lines).Single(s => s.Kind == StockChangeKind.Damage);
            Assert.Equal(ret.Id, damage.SourceStockChangeId);
            Assert.Equal(line.LineId, damage.Lines.Single().SourceLineId);
            Assert.Null(damage.Lines.Single().ReturnSupplierEntityId);   // no supplier known yet: assigned on the damage report
        }

        // The damage refers to the return's line: it cannot be deleted, lowered below 1 or changed to another item; nor the return.
        Assert.Equal(StockChangeRules.DamagedLineCannotBeDeleted, (await Assert.ThrowsAsync<StockChangeRefused>(() => service.DeleteSalesReturnLineAsync(line.LineId))).Message);
        await Assert.ThrowsAsync<StockChangeRefused>(() => service.UpdateSalesReturnLineAsync(line.LineId, seed.ProductId, seed.Variant2Id, 4, 1));
        await Assert.ThrowsAsync<StockChangeRefused>(() => service.UpdateSalesReturnLineAsync(line.LineId, seed.ProductId, seed.Variant1Id, 0, 1));
        Assert.Contains("Damage is recorded on this sales return", (await Assert.ThrowsAsync<StockChangeRefused>(() => service.DeleteSalesReturnAsync(ret.Id))).Message);
    }

    [Fact]
    public async Task A_sales_return_is_in_the_activity_log_with_its_customer_and_lines()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);

        await using var c = db.Open();
        var created = Assert.Single(c.ActivityLog.AsNoTracking().Where(a => a.RecordType == nameof(StockChangeEntity)).ToList());
        Assert.Equal("Created: customer Customer One", created.Text);
        Assert.Equal("Sales return 1 of 05-03-2030", created.RecordLabel);
        Assert.Equal("stock", ActivityLogService.KindOf(created));
    }

    [Fact]
    public async Task Hitens_exchange_example_adds_up_on_one_sales_return()
    {
        // 100 pieces; a customer brings back 10 damaged ones and gets 10 good ones in exchange.
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var c = db.Open())
        {
            c.Purchases.Add(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1,
                PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 100 }] });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await service.AddSalesReturnLineAsync(ret.Id, seed.ProductId, seed.Variant1Id, 10, 1);
        var returned = (await service.DamageCandidatesAsync(DamageSource.SalesReturn, ret.Id)).Single();
        await service.RecordDamageAsync(DamageSource.SalesReturn, ret.Id, [new DamageEntry(returned.LineId, 10, "Damaged in shop")], null);
        await service.AddExchangeLinesAsync(ret.Id, [(seed.ProductId, seed.Variant1Id, 10)]);

        Assert.Equal(new StockFigures(90, 10, 0), await StockOf(db, seed.Variant1Id));   // 90 good on the shelf, 10 damaged

        var loaded = (await service.GetSalesReturnAsync(ret.Id))!;
        var given = Assert.Single(loaded.Lines, l => l.To == StockState.Outside);
        Assert.Equal((StockState.Sellable, 10, 2), (given.From, given.Quantity, given.SerialNumber));   // numbered after the returned line
        Assert.Single(await service.DamageCandidatesAsync(DamageSource.SalesReturn, ret.Id));          // the pop-up lists only what came back
        Assert.Single(new Inventory.Shared.SalesReturnLines(loaded, service).Load());                    // the line editor too
        var row = (await service.SalesReturnsAsync()).Single();
        Assert.Equal((10, 10), (row.Pieces, row.Given));
    }

    [Fact]
    public async Task Goods_given_in_exchange_must_be_more_than_0_and_can_be_removed()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);

        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => service.AddExchangeLinesAsync(ret.Id, [(seed.ProductId, seed.Variant1Id, 2), (seed.ProductId, seed.Variant2Id, 0)]));
        Assert.Equal("The quantity given in exchange must be more than 0.", ex.Message);
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant1Id));   // all or nothing

        await service.AddExchangeLinesAsync(ret.Id, [(seed.ProductId, seed.Variant2Id, 3)]);
        Assert.Equal(new StockFigures(-3, 0, 0), await StockOf(db, seed.Variant2Id));    // not blocked when stock runs out (as sales)
        var given = (await service.GetSalesReturnAsync(ret.Id))!.Lines.Single();
        await service.DeleteSalesReturnLineAsync(given.Id);
        Assert.Equal(new StockFigures(0, 0, 0), await StockOf(db, seed.Variant2Id));
    }

    [Fact]
    public async Task A_line_given_in_exchange_is_named_so_in_the_activity_log()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = new StockChangeService(db.Factory);
        var ret = await service.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await service.AddExchangeLinesAsync(ret.Id, [(seed.ProductId, seed.Variant1Id, 4)]);

        await using var c = db.Open();
        var texts = c.ActivityLog.AsNoTracking().Where(a => a.RecordType == nameof(StockChangeEntity)).Select(a => a.Text).ToList();
        Assert.Contains("Exchange line added: P1 Product One / V1, quantity 4", texts);
    }

    // ---------------------------------------------------------------- screens: switches, menu keys

    [Fact]
    public void Sales_returns_have_their_own_switch_and_Record_damage_its_own()
    {
        Assert.Equal(Permissions.ScreenSalesReturns, Permissions.ForPath("/salesreturns"));
        Assert.Equal(Permissions.ScreenSalesReturns, Permissions.ForPath("/salesreturn/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Contains(Permissions.All, p => p.Key == Permissions.StockDamage && p.Label == "Record damage");
    }

    [Fact]
    public void Esc_and_Alt_N_work_on_sales_returns_without_a_letter_of_their_own()
    {
        Assert.Equal("/salesreturns", Shortcuts.Back("/salesreturn/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Equal("/salesreturns", Shortcuts.Back("/salesreturn"));
        Assert.Equal("/", Shortcuts.Back("/salesreturns"));
        Assert.Equal("/salesreturn", Shortcuts.Target("/salesreturns", 'N', false));
        Assert.DoesNotContain(Shortcuts.Screens, s => s.ListUrl == "/salesreturns");   // no Alt + letter chosen, nothing on the Dashboard list
    }
}
