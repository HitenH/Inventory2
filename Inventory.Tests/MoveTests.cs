using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 3, batches 3-5 and 3-6 (Hiten, 26 Nov 2026): stock transfers between places (the code calls them moves). Nothing is
/// saved until Send; two steps by default (sent, on the way, received), on the way still sellable but reported separately;
/// pieces not received are missing until they turn up or are written off; a starting count (and every transfer with the
/// transit switch off) counts at once; own approval switch; separate Send and Receive switches; "Send to shop" from a purchase.
/// </summary>
public class MoveTests
{
    private static readonly DateOnly Day = new(2030, 5, 6);
    private const int Shop = Places.StartingId;
    private const int Godown = Places.GodownId;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly UserAccess Admin = new("Admin", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now);
    private static readonly UserAccess Sender = new("Sender", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.StockTransferSend }, null)], DateTime.Now);
    private static readonly UserAccess Receiver = new("Receiver", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.StockTransferReceive }, null)], DateTime.Now);

    private static MoveService Moves(TestDatabase db) => new(db.Factory, new ActivityLogService(db.Factory, new CurrentUser { Name = "Admin" }));

    /// <summary>A purchase of <paramref name="qty"/> pieces of V1 (it arrives in the godown).</summary>
    private static async Task<(Guid Purchase, Guid Line)> PurchaseAsync(TestDatabase db, Seed seed, int qty = 10)
    {
        var id = await new PurchaseRepositoryEF(db.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await new PurchaseVariantRepositoryEF(db.Factory).Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = qty });
        await using var c = db.Open();
        return (id, await c.PurchaseVariant.Where(l => l.PurchaseEntityId == id).Select(l => l.Id).SingleAsync(Ct));
    }

    private static async Task<(Dictionary<int, int> Places, StockFigures Total)> StockAsync(TestDatabase db, Guid variant)
    {
        await using var c = db.Open();
        var places = (await StockQuery.SellableByPlaceAsync(c, [variant])).GetValueOrDefault(variant) ?? new Dictionary<int, int>();
        return (places, (await StockQuery.ForVariantsAsync(c, [variant])).GetValueOrDefault(variant));
    }

    private static async Task<StockChangeEntity> SendAsync(MoveService moves, Seed seed, int qty, int from = Godown, int to = Shop, bool startingCount = false, UserAccess? who = null)
    {
        var sent = await moves.SendAsync(from, to, Day, null, startingCount, [(seed.ProductId, seed.Variant1Id, qty, null)], who ?? Admin);
        return (await moves.GetAsync(sent.Id))!;
    }

    private static Guid LineOf(StockChangeEntity move) => MoveService.SentLines(move).Single().Id;

    private static async Task<MoveState> StateAsync(MoveService moves, Guid id)
    {
        var move = (await moves.GetAsync(id))!;
        return MoveService.StateOf(move, await moves.ProgressAsync(move));
    }

    // ---------------------------------------------------------------- 3-5: sending

    [Fact]
    public async Task Sent_pieces_leave_the_godown_and_are_on_the_way_still_sellable_until_the_shop_receives_them()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        var move = await SendAsync(moves, seed, 4);
        Assert.Equal(StockChangeKind.Move, move.Kind);
        Assert.Equal(1, move.VoucherId);
        Assert.Equal(MoveState.OnTheWay, await StateAsync(moves, move.Id));

        var (places, total) = await StockAsync(db, seed.Variant1Id);
        Assert.Equal(6, places[Godown]);
        Assert.Equal(0, places[Shop]);
        Assert.Equal(new StockFigures(10, 0, 0, 4), total);   // Hiten: on the way is sellable, reported separately

        await moves.ReceiveAsync(move.Id, [(LineOf(move), 4)], Admin);
        (places, total) = await StockAsync(db, seed.Variant1Id);
        Assert.Equal(4, places[Shop]);
        Assert.Equal(new StockFigures(10, 0, 0, 0), total);
        Assert.Equal(MoveState.Received, await StateAsync(moves, move.Id));
        Assert.Equal("Admin", (await moves.GetAsync(move.Id))!.ReceivedBy);
    }

    [Fact]
    public async Task Nothing_is_saved_until_Send_and_Send_needs_items_two_places_and_more_than_zero()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendAsync(Godown, Shop, Day, null, false, [], Admin));   // Hiten: no empty transfers
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendAsync(Godown, Godown, Day, null, false, [(seed.ProductId, seed.Variant1Id, 1, null)], Admin));
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendAsync(Godown, 99, Day, null, false, [(seed.ProductId, seed.Variant1Id, 1, null)], Admin));
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendAsync(Godown, Shop, Day, null, false, [(seed.ProductId, seed.Variant1Id, 0, null)], Admin));
        Assert.Empty(await moves.ListAsync());

        var sent = await moves.SendAsync(Godown, Shop, Day, "two items", false,
            [(seed.ProductId, seed.Variant1Id, 3, null), (seed.ProductId, seed.Variant2Id, 1, "fragile")], Admin);
        var loaded = (await moves.GetAsync(sent.Id))!;
        Assert.Equal([(1, 3), (2, 1)], MoveService.SentLines(loaded).Select(l => (l.SerialNumber, l.Quantity)));
        Assert.Equal("two items", loaded.Remarks);
    }

    [Fact]
    public async Task Send_and_Receive_are_separate_switches_and_someone_with_both_does_both()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        await Assert.ThrowsAsync<StockChangeRefused>(() => SendAsync(moves, seed, 2, who: Receiver));
        var move = await SendAsync(moves, seed, 2, who: Sender);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.ReceiveAsync(move.Id, [(LineOf(move), 2)], Sender));
        await moves.ReceiveAsync(move.Id, [(LineOf(move), 2)], Receiver);
        Assert.Equal(MoveState.Received, await StateAsync(moves, move.Id));

        Assert.True(Admin.MayUseTransfers);
        Assert.True(Sender.MayUseTransfers && Receiver.MayUseTransfers);
        Assert.True(Sender.AllowsPath("/transfers") && Receiver.AllowsPath("/transfer/1"));
        var clerk = new UserAccess("Clerk", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.ScreenSales }, null)], DateTime.Now);
        Assert.False(clerk.AllowsPath("/transfers"));
    }

    [Fact]
    public async Task A_scanned_label_is_found_whatever_the_capitals()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var moves = Moves(db);
        var item = await moves.FindItemAsync(" p1 ", "v2");
        Assert.Equal((seed.ProductId, seed.Variant2Id, "P1", "V2"), (item!.ProductEntityId, item.VariantEntityId, item.ProductId, item.VariantId));
        Assert.Null(await moves.FindItemAsync("P1", "V9"));
        Assert.Null(await moves.FindItemAsync("", "V1"));
    }

    [Fact]
    public async Task A_starting_count_and_any_transfer_with_two_steps_switched_off_count_in_the_new_place_at_once()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var c = db.Open())
        {
            c.Purchases.Add(new PurchaseEntity
            {
                Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 9,   // old stock: in the shop
                PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 20 }],
            });
            await c.SaveChangesAsync(Ct);
        }

        var moves = Moves(db);
        var count = await SendAsync(moves, seed, 8, from: Shop, to: Godown, startingCount: true);
        Assert.Equal(StockChangeKind.StartingCount, count.Kind);
        Assert.Equal(MoveState.Moved, await StateAsync(moves, count.Id));
        Assert.Equal((12, 8), ((await StockAsync(db, seed.Variant1Id)).Places[Shop], (await StockAsync(db, seed.Variant1Id)).Places[Godown]));

        await moves.SetTransitAsync(false, Admin);
        var atOnce = await SendAsync(moves, seed, 3);
        Assert.Equal(StockChangeKind.MoveAtOnce, atOnce.Kind);
        Assert.Equal(2, atOnce.VoucherId);   // transfers and starting counts share the day's numbers
        var (places, total) = await StockAsync(db, seed.Variant1Id);
        Assert.Equal((15, 5, 0), (places[Shop], places[Godown], total.OnTheWay));
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.ReceiveAsync(atOnce.Id, [], Admin));
    }

    [Fact]
    public async Task With_transfer_approval_on_a_transfer_waits_moves_nothing_and_cannot_be_received_until_approved()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        await moves.SetApprovalAsync(true, Admin);
        var move = await SendAsync(moves, seed, 4);
        Assert.Equal(StockChangeStatus.Waiting, move.Status);
        Assert.Equal(10, (await StockAsync(db, seed.Variant1Id)).Places[Godown]);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.ReceiveAsync(move.Id, [(LineOf(move), 4)], Admin));

        await new StockEntryService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser())).DecideAsync(move.Id, true, Admin);
        Assert.Equal(6, (await StockAsync(db, seed.Variant1Id)).Places[Godown]);
        await moves.ReceiveAsync(move.Id, [(LineOf(move), 4)], Admin);
        Assert.Equal(4, (await StockAsync(db, seed.Variant1Id)).Places[Shop]);
    }

    [Fact]
    public async Task Only_the_Admin_changes_the_switches()
    {
        using var db = new TestDatabase();
        var moves = Moves(db);
        Assert.Equal((true, false), await moves.SwitchesAsync());   // Hiten: two steps on, approval off
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SetTransitAsync(false, Sender));
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SetApprovalAsync(true, Sender));
    }

    [Fact]
    public async Task Only_the_Admin_changes_the_places_of_a_sent_transfer_and_only_before_anything_was_received()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var store = await new PlaceService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser()), new CurrentUser()).AddPlaceAsync("Store");
        var moves = Moves(db);
        var move = await SendAsync(moves, seed, 2);

        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.ChangePlacesAsync(move.Id, Godown, store.Id, Sender));
        await moves.ChangePlacesAsync(move.Id, Godown, store.Id, Admin);
        await moves.ReceiveAsync(move.Id, [(LineOf(move), 2)], Admin);
        Assert.Equal(2, (await StockAsync(db, seed.Variant1Id)).Places[store.Id]);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.ChangePlacesAsync(move.Id, Godown, Shop, Admin));
    }

    [Fact]
    public async Task A_transfer_can_be_deleted_until_something_was_received()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        var keep = await SendAsync(moves, seed, 2);
        var drop = await SendAsync(moves, seed, 1);
        await moves.DeleteAsync(drop.Id);
        Assert.Equal(8, (await StockAsync(db, seed.Variant1Id)).Places[Godown]);

        await moves.ReceiveAsync(keep.Id, [(LineOf(keep), 1)], Admin);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.DeleteAsync(keep.Id));
    }

    // ---------------------------------------------------------------- 3-6: receiving, missing pieces

    [Fact]
    public async Task Pieces_not_received_are_missing_until_they_turn_up_or_are_written_off()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        var move = await SendAsync(moves, seed, 5);
        var line = LineOf(move);

        await moves.ReceiveAsync(move.Id, [(line, 3)], Admin);
        Assert.Equal(MoveState.Missing, await StateAsync(moves, move.Id));
        Assert.Equal(1, await moves.MissingCountAsync());
        var item = (await moves.ListAsync()).Single();
        Assert.Equal((5, 3, 2), (item.Sent, item.Received, item.Missing));
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.ReceiveAsync(move.Id, [(line, 3)], Admin));   // only 2 still to come

        await moves.ReceiveAsync(move.Id, [(line, 1)], Admin);   // one turned up later
        Assert.Equal(1, (await StockAsync(db, seed.Variant1Id)).Total.OnTheWay);

        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.WriteOffMissingAsync(move.Id, "lost", Sender));   // Admin only by default
        var writeOff = await moves.WriteOffMissingAsync(move.Id, "lost", Admin);
        Assert.Equal(StockChangeKind.WriteOff, writeOff.Kind);
        Assert.Equal(MoveState.Received, await StateAsync(moves, move.Id));
        Assert.Equal(0, await moves.MissingCountAsync());
        var (places, total) = await StockAsync(db, seed.Variant1Id);
        Assert.Equal(new StockFigures(9, 0, 0, 0), total);   // 10 bought, 1 lost on the way
        Assert.Equal(4, places[Shop]);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.WriteOffMissingAsync(move.Id, null, Admin));   // nothing missing now
    }

    [Fact]
    public async Task A_write_off_of_missing_pieces_waits_for_approval_when_stock_changes_need_approval()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        var moves = Moves(db);
        var move = await SendAsync(moves, seed, 2);
        await moves.ReceiveAsync(move.Id, [], Admin);   // nothing came
        Assert.Equal(MoveState.Missing, await StateAsync(moves, move.Id));

        await new StockEntryService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser())).SetApprovalAsync(true, Admin);
        var writeOff = await moves.WriteOffMissingAsync(move.Id, null, Admin);
        Assert.Equal(StockChangeStatus.Waiting, writeOff.Status);
        Assert.Equal(MoveState.Received, await StateAsync(moves, move.Id));     // no longer counted as missing
        Assert.Equal(2, (await StockAsync(db, seed.Variant1Id)).Total.OnTheWay); // but the pieces leave only when approved
    }

    // ---------------------------------------------------------------- 3-5: Send to shop from a purchase

    [Fact]
    public async Task Send_to_shop_makes_a_transfer_from_where_the_purchase_arrived_linked_to_it()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var (purchase, line) = await PurchaseAsync(db, seed);
        var moves = Moves(db);
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendFromPurchaseAsync(purchase, [(line, 11)], Admin));   // only 10 came in
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendFromPurchaseAsync(purchase, [(line, 0)], Admin));
        await Assert.ThrowsAsync<StockChangeRefused>(() => moves.SendFromPurchaseAsync(purchase, [(line, 1)], Receiver));

        var move = await moves.SendFromPurchaseAsync(purchase, [(line, 4)], Admin);
        Assert.Equal((Godown, Shop, purchase), (move.PlaceId!.Value, move.ToPlaceId!.Value, move.PurchaseEntityId!.Value));
        Assert.Equal(4, (await moves.PurchaseSendLinesAsync(purchase)).Single().AlreadySent);
        Assert.Equal(6, (await StockAsync(db, seed.Variant1Id)).Places[Godown]);

        // The purchase cannot be deleted while goods of it were sent on; changing its place leaves the transfer alone.
        var ex = await Assert.ThrowsAsync<StockChangeRefused>(() => new PurchaseRepositoryEF(db.Factory).Delete(new PurchaseEntity { Id = purchase }));
        Assert.Contains("sent on to another place", ex.Message);
        var places = new PlaceService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser()), new CurrentUser());
        await places.ChangePurchasePlaceAsync(purchase, Shop, Admin);
        Assert.Equal(Godown, (await moves.GetAsync(move.Id))!.PlaceId);
    }

    // ---------------------------------------------------------------- 3-6: the list, below zero

    [Fact]
    public async Task The_list_shows_transfers_from_or_to_the_persons_places_and_below_zero_shows_what_was_sold_before_the_transfer()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await PurchaseAsync(db, seed);
        await using (var c = db.Open())
        {
            c.Sales.Add(new SalesEntity
            {
                Date = Day, CustomerEntityId = seed.CustomerId, VoucherId = 1,   // sold in the shop before anything was moved there
                SalesVariants = [new SalesVariantEntity { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2 }],
            });
            await c.SaveChangesAsync(Ct);
        }

        var moves = Moves(db);
        var below = Assert.Single(await moves.BelowZeroAsync());
        Assert.Equal(("V1", "Shop", -2), (below.VariantId, below.Place, below.Qty));
        Assert.Empty(await moves.BelowZeroAsync(new HashSet<int> { Godown }));   // a godown person does not see the shop

        var move = await SendAsync(moves, seed, 2);
        await moves.ReceiveAsync(move.Id, [(LineOf(move), 2)], Admin);
        Assert.Empty(await moves.BelowZeroAsync());   // the transfer was recorded: back to 0

        var store = await new PlaceService(db.Factory, new ActivityLogService(db.Factory, new CurrentUser()), new CurrentUser()).AddPlaceAsync("Store");
        Assert.Single(await moves.ListAsync(new HashSet<int> { Godown }));
        Assert.Empty(await moves.ListAsync(new HashSet<int> { store.Id }));
    }

    [Fact]
    public void The_Stock_transfers_screens_open_with_Send_or_Receive_and_Esc_goes_back_to_the_list()
    {
        Assert.Equal(Permissions.StockTransfers, Permissions.ForPath("/transfers"));
        Assert.Equal(Permissions.StockTransfers, Permissions.ForPath("/transfer/123"));
        Assert.Equal("/transfers", Shortcuts.Back("/transfer/123"));
        Assert.Contains(Permissions.All, p => p.Key == Permissions.StockTransferSend && p.Group == Permissions.StockGroup);
        Assert.Contains(Permissions.All, p => p.Key == Permissions.StockTransferReceive && p.Group == Permissions.StockGroup);
        Assert.Contains(Permissions.All, p => p.Key == Permissions.StockTransferWriteOff && p.Group == Permissions.StockGroup);
        Assert.DoesNotContain(Permissions.All, p => p.Key == Permissions.StockTransfers);   // not a switch of its own
    }
}
