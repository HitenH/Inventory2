using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 2, batches 2-5 and 2-6: the damage report (assign supplier, ready to send, repaired, written off, sent) and purchase
/// returns. Rules in Service/DamagedGoodsService.cs.
/// </summary>
public class DamageReportTests
{
    private static readonly DateOnly Day = new(2030, 4, 2);

    /// <summary>A purchase from the seed supplier: V1 × 10 and V2 × 6, with 4 of V1 and 2 of V2 recorded as damaged.</summary>
    private static async Task<(Guid Purchase, Guid Line1, Guid Line2)> DamagedPurchaseAsync(TestDatabase db, Seed seed)
    {
        var purchases = new PurchaseRepositoryEF(db.Factory);
        var lines = new PurchaseVariantRepositoryEF(db.Factory);
        var id = await purchases.Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 7 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 10 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, SerialNumber = 2, Quantity = 6 });
        Guid l1, l2;
        await using (var c = db.Open())
        {
            var ids = c.PurchaseVariant.Where(l => l.PurchaseEntityId == id).OrderBy(l => l.SerialNumber).Select(l => l.Id).ToList();
            (l1, l2) = (ids[0], ids[1]);
        }

        await new StockChangeService(db.Factory).RecordDamageAsync(DamageSource.Purchase, id,
            [new DamageEntry(l1, 4, "Broken in transit"), new DamageEntry(l2, 2, "Manufacturing defect")], null);
        return (id, l1, l2);
    }

    private static async Task<StockFigures> StockOf(TestDatabase db, Guid variant) =>
        (await new ProductRepositoryEF(db.Factory).GetStockByVariantIdsAsync([variant]))[variant];

    private static async Task<Guid> OtherSupplierAsync(TestDatabase db)
    {
        await using var c = db.Open();
        var s = new SupplierEntity { SupplierId = "S2", Name = "Repairs Ltd" };
        c.Suppliers.Add(s);
        await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        return s.Id;
    }

    // ---------------------------------------------------------------- 2-5: the damage report

    [Fact]
    public async Task The_report_lists_what_is_lying_in_damage_one_row_per_purchase_line()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, line2) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);

        var lots = await service.LotsAsync();
        Assert.Equal(2, lots.Count);
        var v1 = lots.Single(l => l.VariantId == "V1");
        Assert.Equal((line1, 4, "Broken in transit", seed.SupplierId, "Supplier One", false, "Supplier assigned"),
            (v1.Key, v1.Pieces, v1.Reasons, v1.SupplierId!.Value, v1.SupplierName, v1.ReadyToSend, v1.Status));
        Assert.StartsWith("Purchase 7 of 02-04-2030, Supplier One", v1.From);
        Assert.Equal($"/purchase/{purchase}?from=damage", v1.FromLink);   // Esc there comes back to the report (Hiten)
        Assert.Equal(2, lots.Single(l => l.Key == line2).Pieces);

        // More damage on the same line later, and some found not damaged after all: still one row with the net pieces.
        await new StockChangeService(db.Factory).RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 3, null)], null);
        Assert.Equal(3, (await service.LotsAsync()).Single(l => l.Key == line1).Pieces);
        await new StockChangeService(db.Factory).RecordDamageAsync(DamageSource.Purchase, purchase, [new DamageEntry(line1, 0, null)], null);
        Assert.DoesNotContain(await service.LotsAsync(), l => l.Key == line1);
    }

    [Fact]
    public async Task A_supplier_can_be_assigned_and_rows_marked_ready_to_send()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (_, line1, line2) = await DamagedPurchaseAsync(db, seed);
        var other = await OtherSupplierAsync(db);
        var service = new DamagedGoodsService(db.Factory);

        await service.AssignSupplierAsync([line1], other);
        await service.SetReadyAsync([line1, line2], true);
        var lots = await service.LotsAsync();
        Assert.Equal(("Repairs Ltd", true, "Ready to send"), (lots.Single(l => l.Key == line1).SupplierName, lots.Single(l => l.Key == line1).ReadyToSend, lots.Single(l => l.Key == line1).Status));
        Assert.True(lots.Single(l => l.Key == line2).ReadyToSend);

        await service.SetReadyAsync([line2], false);
        Assert.False((await service.LotsAsync()).Single(l => l.Key == line2).ReadyToSend);
        await Assert.ThrowsAsync<StockChangeRefused>(() => service.SetReadyAsync([], true));
        await Assert.ThrowsAsync<StockChangeRefused>(() => service.AssignSupplierAsync([line1], Guid.Empty));
    }

    [Fact]
    public async Task Goods_without_a_supplier_cannot_be_made_ready_or_sent()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var returns = new StockChangeService(db.Factory);
        var ret = await returns.CreateSalesReturnAsync(seed.CustomerId, Day, null);
        await returns.AddSalesReturnLineAsync(ret.Id, seed.ProductId, seed.Variant1Id, 3, 1);
        var returned = (await returns.DamageCandidatesAsync(DamageSource.SalesReturn, ret.Id)).Single();
        await returns.RecordDamageAsync(DamageSource.SalesReturn, ret.Id, [new DamageEntry(returned.LineId, 3, "Damaged in shop")], null);
        var service = new DamagedGoodsService(db.Factory);

        var lot = Assert.Single(await service.LotsAsync());
        Assert.Equal((returned.LineId, (Guid?)null, "No supplier yet", $"/salesreturn/{ret.Id}?from=damage"), (lot.Key, lot.SupplierId, lot.Status, lot.FromLink));
        Assert.StartsWith("Sales return 1 of 02-04-2030, Customer One", lot.From);
        Assert.Contains("Assign a supplier first", (await Assert.ThrowsAsync<StockChangeRefused>(() => service.SetReadyAsync([lot.Key], true))).Message);
        Assert.Contains("Assign a supplier first", (await Assert.ThrowsAsync<StockChangeRefused>(() => service.SendBackAsync([new LotPieces(lot.Key, 3)]))).Message);
    }

    [Fact]
    public async Task Repaired_pieces_go_back_to_sellable_and_written_off_ones_leave_stock()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line1, _) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);
        Assert.Equal(new StockFigures(6, 4, 0), await StockOf(db, seed.Variant1Id));

        var repair = await service.RepairAsync([new LotPieces(line1, 1)]);
        Assert.Equal(StockChangeKind.Repaired, repair.Kind);
        Assert.Equal(new StockFigures(7, 3, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(3, (await service.LotsAsync()).Single(l => l.Key == line1).Pieces);

        await service.WriteOffAsync([new LotPieces(line1, 3)]);
        Assert.Equal(new StockFigures(7, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.DoesNotContain(await service.LotsAsync(), l => l.Key == line1);

        // The pop-up on the purchase now cannot lower that line below the 4 pieces already dealt with.
        var candidate = (await new StockChangeService(db.Factory).DamageCandidatesAsync(DamageSource.Purchase, purchase)).Single(c => c.LineId == line1);
        Assert.Equal((4, 4), (candidate.Damaged, candidate.Lowest));
    }

    [Theory]
    [InlineData(5, "Only 4 pieces are lying in damage")]
    [InlineData(0, "more than 0")]
    public async Task More_than_is_lying_in_damage_or_nothing_is_refused(int pieces, string message)
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (_, line1, _) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);

        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => service.RepairAsync([new LotPieces(line1, pieces)]));
        Assert.Contains(message, ex.Message);
        Assert.Equal(new StockFigures(6, 4, 0), await StockOf(db, seed.Variant1Id));
    }

    // ---------------------------------------------------------------- 2-6: purchase returns

    [Fact]
    public async Task Sent_makes_one_purchase_return_for_the_supplier_and_takes_the_goods_off_the_damaged_count()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (_, line1, line2) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);

        var made = await service.SendBackAsync([new LotPieces(line1, 4), new LotPieces(line2, 1)], "courier");
        Assert.Equal((StockChangeKind.PurchaseReturn, seed.SupplierId, 1, "courier"), (made.Kind, made.SupplierEntityId!.Value, made.VoucherId, made.Remarks));
        Assert.Equal(new StockFigures(6, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal(new StockFigures(4, 1, 0), await StockOf(db, seed.Variant2Id));
        var left = Assert.Single(await service.LotsAsync());
        Assert.Equal((line2, 1), (left.Key, left.Pieces));

        var loaded = (await service.GetPurchaseReturnAsync(made.Id))!;
        Assert.Equal("Supplier One", loaded.Supplier!.Name);
        Assert.All(loaded.Lines, l => Assert.Equal((StockState.Damaged, StockState.Outside), (l.From, l.To)));
        var sources = await service.SourcesAsync(loaded);
        Assert.All(sources.Values, s => Assert.Equal("Purchase 7 of 02-04-2030", s));

        var listed = Assert.Single(await service.PurchaseReturnsAsync());
        Assert.Equal((5, 0, "Supplier One"), (listed.FromDamage, listed.GoodStock, listed.SupplierName));
    }

    [Fact]
    public async Task Goods_for_different_suppliers_are_sent_one_supplier_at_a_time()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (_, line1, line2) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);
        await service.AssignSupplierAsync([line2], await OtherSupplierAsync(db));

        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => service.SendBackAsync([new LotPieces(line1, 4), new LotPieces(line2, 2)]));
        Assert.Contains("different suppliers", ex.Message);
        Assert.Equal(2, (await service.LotsAsync()).Count);
    }

    [Fact]
    public async Task Removing_a_line_or_deleting_the_return_puts_the_goods_back_on_the_damage_report()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (_, line1, line2) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);
        var made = await service.SendBackAsync([new LotPieces(line1, 4), new LotPieces(line2, 2)]);

        var first = (await service.GetPurchaseReturnAsync(made.Id))!.Lines.OrderBy(l => l.SerialNumber).First();
        await service.DeletePurchaseReturnLineAsync(first.Id);
        Assert.Single(await service.LotsAsync());
        await service.DeletePurchaseReturnAsync(made.Id);
        Assert.Equal(2, (await service.LotsAsync()).Count);
        Assert.Equal(new StockFigures(6, 4, 0), await StockOf(db, seed.Variant1Id));
    }

    [Fact]
    public async Task A_purchase_return_can_be_made_directly_for_good_stock()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var c = db.Open())
        {
            c.Purchases.Add(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1,
                PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 20 }] });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var service = new DamagedGoodsService(db.Factory);
        var ret = await service.CreatePurchaseReturnAsync(seed.SupplierId, Day, " wrong colour ");
        await service.AddPurchaseReturnLinesAsync(ret.Id, [(seed.ProductId, seed.Variant1Id, 5, 1)]);
        Assert.Equal(new StockFigures(15, 0, 0), await StockOf(db, seed.Variant1Id));
        var line = (await service.GetPurchaseReturnAsync(ret.Id))!.Lines.Single();
        Assert.Equal((StockState.Sellable, StockState.Outside), (line.From, line.To));
        Assert.Single(new Inventory.Shared.PurchaseReturnLines((await service.GetPurchaseReturnAsync(ret.Id))!, service).Load());

        await service.UpdatePurchaseReturnLineAsync(line.Id, seed.ProductId, seed.Variant1Id, 2, 1);
        Assert.Equal(new StockFigures(18, 0, 0), await StockOf(db, seed.Variant1Id));
        Assert.Equal("The quantity sent back must be more than 0.",
            (await Assert.ThrowsAsync<StockChangeRefused>(() => service.AddPurchaseReturnLinesAsync(ret.Id, [(seed.ProductId, seed.Variant1Id, 0, 2)]))).Message);
        await service.UpdatePurchaseReturnAsync(ret.Id, seed.SupplierId, Day.AddDays(1), "changed");
        Assert.Equal("changed", (await service.GetPurchaseReturnAsync(ret.Id))!.Remarks);
        Assert.Equal((0, 2), ((await service.PurchaseReturnsAsync()).Single().FromDamage, (await service.PurchaseReturnsAsync()).Single().GoodStock));
    }

    [Fact]
    public async Task Purchase_returns_and_damage_report_actions_are_in_the_activity_log()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (_, line1, _) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);
        var other = await OtherSupplierAsync(db);
        await service.AssignSupplierAsync([line1], other);
        var made = await service.SendBackAsync([new LotPieces(line1, 4)]);

        await using var c = db.Open();
        var log = c.ActivityLog.AsNoTracking().Where(a => a.RecordType == nameof(StockChangeEntity)).OrderBy(a => a.Id).Select(a => a.Text).ToList();
        Assert.Contains(log, t => t.StartsWith("Line P1 Product One / V1: Return supplier Supplier One → Repairs Ltd"));
        Assert.Contains("Created: supplier Repairs Ltd, 1 line, 4 pieces", log);
        Assert.DoesNotContain(log, t => t.StartsWith("Exchange line"));   // a purchase return has no exchange lines
        Assert.Equal($"Purchase return {made.VoucherId} of {made.Date:dd-MM-yyyy}", c.ActivityLog.AsNoTracking().Single(a => a.RecordId == made.Id.ToString() && a.Action == "Created").RecordLabel);
    }

    [Fact]
    public async Task The_damage_report_has_one_activity_panel_with_all_its_history()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        db.CurrentUser.Name = "Hiten";
        var (_, line1, line2) = await DamagedPurchaseAsync(db, seed);
        var service = new DamagedGoodsService(db.Factory);
        await service.AssignSupplierAsync([line2], await OtherSupplierAsync(db));
        await service.SetReadyAsync([line1], true);
        await service.RepairAsync([new LotPieces(line1, 1)]);
        await service.SendBackAsync([new LotPieces(line1, 3)]);
        var returns = new StockChangeService(db.Factory);
        await returns.CreateSalesReturnAsync(seed.CustomerId, Day, null);   // a sales return is not damage history

        var everything = new UserAccess("Hiten", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now);
        var history = await new ActivityLogService(db.Factory, new CurrentUser { Name = "Hiten" }).ForDamageReportAsync(everything);
        var firstLines = history.Groups.Select(g => g.Lines[0]).ToList();
        Assert.Equal("Damage report", history.Label);
        Assert.Contains(firstLines, l => l.StartsWith("Damage 1 of ") && l.Contains("Created: damage on Purchase 7 of 02-04-2030"));
        Assert.Contains(firstLines, l => l.StartsWith("Damage 1 of ") && l.Contains("Return supplier Supplier One → Repairs Ltd"));
        Assert.Contains(firstLines, l => l.StartsWith("Damage 1 of ") && l.Contains("Ready to send no → yes"));
        Assert.Contains(firstLines, l => l.StartsWith("Repair 1 of ") && l.Contains("Created: repair"));
        Assert.Contains(firstLines, l => l.StartsWith("Purchase return 1 of ") && l.Contains("Created: supplier Supplier One"));
        Assert.DoesNotContain(firstLines, l => l.StartsWith("Sales return"));
        Assert.All(history.Groups, g => Assert.Equal("Hiten", g.By));

        var nobody = await new ActivityLogService(db.Factory, new CurrentUser()).ForDamageReportAsync(UserAccess.Nobody);
        Assert.Empty(nobody.Groups);   // needs the "See activity log" switch
    }

    // ---------------------------------------------------------------- screens

    [Fact]
    public void The_new_screens_have_their_switches_menu_keys_and_Esc()
    {
        Assert.Equal(Permissions.ScreenDamageReport, Permissions.ForPath("/damage"));
        Assert.Equal(Permissions.ScreenPurchaseReturns, Permissions.ForPath("/purchasereturns"));
        Assert.Equal(Permissions.ScreenPurchaseReturns, Permissions.ForPath("/purchasereturn/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Equal("/purchasereturns", Shortcuts.Back("/purchasereturn/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Equal("/purchasereturn", Shortcuts.Target("/purchasereturns", 'N', false));
        Assert.Equal("/", Shortcuts.Back("/damage"));
        // Hiten, 26 Nov 2026: a purchase or sales return opened from the damage report goes back to it with Esc.
        Assert.Equal("/damage", Shortcuts.Back("/purchase/2f1c2d3e-0000-0000-0000-000000000000?from=damage"));
        Assert.Equal("/damage", Shortcuts.Back("/salesreturn/2f1c2d3e-0000-0000-0000-000000000000?from=damage"));
        Assert.Equal("/purchases", Shortcuts.Back("/purchase/2f1c2d3e-0000-0000-0000-000000000000"));
        Assert.Equal("/labels?purchase=2f1c2d3e-0000-0000-0000-000000000000", Shortcuts.Target("/purchase/2f1c2d3e-0000-0000-0000-000000000000?from=damage", 'L', false));
        Assert.Null(Shortcuts.Target("/damage", 'N', false));
    }
}
