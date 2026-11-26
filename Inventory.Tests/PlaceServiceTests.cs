using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Stage 3, batch 3-2 (Hiten, 26 Nov 2026): addresses (02-03-B, typed as 0203B), places and racks kept from Settings,
/// and where each variant is kept (several racks in several places, one main rack per place).
/// </summary>
public class PlaceServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PlaceService Service(TestDatabase db, string user = "Admin")
    {
        var who = new CurrentUser { Name = user };
        return new PlaceService(db.Factory, new ActivityLogService(db.Factory, who), who);
    }

    [Theory]
    [InlineData("0203B", 2, 3, 'B')]
    [InlineData("02-03-B", 2, 3, 'B')]
    [InlineData("2-3-b", 2, 3, 'B')]
    [InlineData(" 02 03 b ", 2, 3, 'B')]
    [InlineData("2/13/A", 2, 13, 'A')]
    [InlineData("9901Z", 99, 1, 'Z')]
    public void An_address_can_be_typed_several_ways_and_gives_the_same_result(string typed, int place, int rack, char level)
    {
        Assert.True(RackAddress.TryParse(typed, out var address));
        Assert.Equal(new RackAddress(place, rack, level), address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("203B")]        // without dashes it must be two digits, two digits, a letter
    [InlineData("0203")]
    [InlineData("02-03")]
    [InlineData("00-03-B")]     // numbers start at 1
    [InlineData("02-100-B")]
    [InlineData("02-03-BB")]
    [InlineData("02-03-1")]
    [InlineData("AJ0120101")]   // a product code is never taken for an address
    public void A_wrong_address_is_refused(string typed) => Assert.False(RackAddress.TryParse(typed, out _));

    [Fact]
    public void An_address_is_shown_with_dashes_and_its_code_is_what_a_person_types()
    {
        var address = new RackAddress(2, 3, 'B');
        Assert.Equal("02-03-B", address.Text);
        Assert.Equal("0203B", address.Code);
        Assert.Equal('A', RackAddress.LevelLetter(1));
        Assert.Equal(5, RackAddress.LevelNumber('e'));
        Assert.Equal("Shop, Rack 1, Level B", RackAddress.Describe("Shop", new RackAddress(1, 1, 'B')));   // Hiten: in full words
    }

    [Fact]
    public void Places_and_racks_open_with_the_settings_switch_and_Esc_goes_back_to_Settings()
    {
        Assert.Equal(Permissions.AdminSettings, Permissions.ForPath("/places"));
        Assert.Equal("/settings", Shortcuts.Back("/places"));
        Assert.Contains(Permissions.All, p => p.Key == Permissions.StockRacks && p.Group == Permissions.StockGroup);
    }

    [Fact]
    public async Task Places_can_be_added_renamed_and_renumbered_while_they_have_no_racks()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        var added = await service.AddPlaceAsync("  Second godown ");
        Assert.Equal(3, added.Number);   // the lowest free number
        Assert.Equal("Second godown", added.Name);
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddPlaceAsync("GODOWN"));   // names unique ignoring capitals
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddPlaceAsync("  "));

        await service.RenamePlaceAsync(added.Id, "Back store");
        await service.RenumberPlaceAsync(added.Id, 7);
        await Assert.ThrowsAsync<PlaceRefused>(() => service.RenumberPlaceAsync(added.Id, 2));   // the godown has 02

        await service.AddRacksAsync(added.Id, 1, 1, 3);
        var ex = await Assert.ThrowsAsync<PlaceRefused>(() => service.RenumberPlaceAsync(added.Id, 8));
        Assert.Contains("number stays 07", ex.Message);

        var places = await service.AllAsync();
        Assert.Equal(["01 Shop", "02 Godown", "07 Back store"], places.Select(p => $"{p.NumberText} {p.Name}"));
    }

    [Fact]
    public async Task The_shop_a_place_with_racks_and_a_place_with_entries_cannot_be_deleted()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = Service(db);
        await Assert.ThrowsAsync<PlaceRefused>(() => service.DeletePlaceAsync(Places.StartingId));

        var store = await service.AddPlaceAsync("Store");
        await service.AddRacksAsync(store.Id, 1, 2, 2);
        await Assert.ThrowsAsync<PlaceRefused>(() => service.DeletePlaceAsync(store.Id));

        await using (var c = db.Open())
        {
            c.Purchases.Add(new PurchaseEntity
            {
                Date = new DateOnly(2030, 1, 1), SupplierEntityId = seed.SupplierId, VoucherId = 1, PlaceId = Places.GodownId,
                PurchaseVariants = [new PurchaseVariant { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, SerialNumber = 1, Quantity = 1 }],
            });
            await c.SaveChangesAsync(Ct);
        }

        var ex = await Assert.ThrowsAsync<PlaceRefused>(() => service.DeletePlaceAsync(Places.GodownId));
        Assert.Contains("holds stock entries", ex.Message);

        var empty = await service.AddPlaceAsync("Empty room");
        await service.DeletePlaceAsync(empty.Id);
        Assert.DoesNotContain(await service.AllAsync(), p => p.Name == "Empty room");
    }

    [Fact]
    public async Task Racks_are_added_in_a_range_skipping_those_already_there()
    {
        using var db = new TestDatabase();
        var service = Service(db);
        Assert.Equal(3, await service.AddRacksAsync(Places.GodownId, 1, 3, 5));
        Assert.Equal(2, await service.AddRacksAsync(Places.GodownId, 3, 5, 4));   // 03 is already there
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddRacksAsync(Places.GodownId, 1, 2, 4));   // nothing new
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddRacksAsync(Places.GodownId, 6, 100, 4));
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddRacksAsync(Places.GodownId, 7, 6, 4));
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddRacksAsync(Places.GodownId, 6, 6, 27));

        var godown = (await service.AllAsync()).Single(p => p.Id == Places.GodownId);
        Assert.Equal(["01 A–E", "02 A–E", "03 A–E", "04 A–D", "05 A–D"], godown.Racks.Select(r => $"{r.Number:00} {r.LevelsText}"));
    }

    [Fact]
    public async Task A_rack_or_level_that_holds_items_cannot_be_taken_away()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = Service(db);
        await service.AddRacksAsync(Places.GodownId, 1, 1, 5);
        var rack = (await service.AllAsync()).Single(p => p.Id == Places.GodownId).Racks.Single();
        await service.AddVariantRackAsync(seed.Variant1Id, rack.Id, 'D', makeMain: false);

        await service.SetRackLevelsAsync(rack.Id, 4);   // E goes, D stays
        var ex = await Assert.ThrowsAsync<PlaceRefused>(() => service.SetRackLevelsAsync(rack.Id, 3));
        Assert.Contains("Level D of rack 02-01 still holds items", ex.Message);
        ex = await Assert.ThrowsAsync<PlaceRefused>(() => service.DeleteRackAsync(rack.Id));
        Assert.Contains("still holds 1 item", ex.Message);

        var racks = await service.RacksOfVariantsAsync([seed.Variant1Id]);
        await service.RemoveVariantRackAsync(racks[seed.Variant1Id].Single().Id);
        await service.DeleteRackAsync(rack.Id);
        Assert.Empty((await service.AllAsync()).Single(p => p.Id == Places.GodownId).Racks);
    }

    [Fact]
    public void An_address_is_found_among_the_places_or_says_plainly_why_not()
    {
        IReadOnlyList<PlaceItem> places =
        [
            new(1, 1, "Shop", [new RackItem(10, 3, 4, 0)]),
            new(2, 2, "Godown", [new RackItem(20, 3, 2, 0)]),
        ];
        Assert.Equal(20, PlaceService.Resolve(places, new RackAddress(2, 3, 'B')).Rack!.Id);
        Assert.Equal("There is no place 05.", PlaceService.Resolve(places, new RackAddress(5, 3, 'A')).Problem);
        Assert.Equal("Godown has no rack 31.", PlaceService.Resolve(places, new RackAddress(2, 31, 'A')).Problem);
        Assert.Equal("Rack 02-03 has levels A–B.", PlaceService.Resolve(places, new RackAddress(2, 3, 'C')).Problem);
    }

    [Fact]
    public async Task A_variant_has_one_main_rack_per_place_the_first_one_added_unless_another_is_made_main()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = Service(db);
        await service.AddRacksAsync(Places.GodownId, 1, 7, 5);
        await service.AddRacksAsync(Places.StartingId, 1, 3, 4);
        var all = await service.AllAsync();
        int RackId(int place, int number) => all.Single(p => p.Id == place).Racks.Single(r => r.Number == number).Id;

        await service.AddVariantRackAsync(seed.Variant1Id, RackId(Places.GodownId, 5), 'A', makeMain: false);   // first in the godown: main
        await service.AddVariantRackAsync(seed.Variant1Id, RackId(Places.GodownId, 7), 'C', makeMain: false);
        await service.AddVariantRackAsync(seed.Variant1Id, RackId(Places.StartingId, 3), 'b', makeMain: false);  // first in the shop: main
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddVariantRackAsync(seed.Variant1Id, RackId(Places.GodownId, 7), 'C', makeMain: false));
        await Assert.ThrowsAsync<PlaceRefused>(() => service.AddVariantRackAsync(seed.Variant1Id, RackId(Places.StartingId, 1), 'E', makeMain: false));

        async Task<string> Shown() => string.Join(" ", (await service.RacksOfVariantsAsync([seed.Variant1Id]))[seed.Variant1Id]
            .Select(r => (r.IsMain ? "*" : "") + r.Address));
        Assert.Equal("*01-03-B *02-05-A 02-07-C", await Shown());

        await service.AddVariantRackAsync(seed.Variant1Id, RackId(Places.GodownId, 2), 'A', makeMain: true);
        Assert.Equal("*01-03-B *02-02-A 02-05-A 02-07-C", await Shown());

        var rows = (await service.RacksOfVariantsAsync([seed.Variant1Id]))[seed.Variant1Id];
        await service.SetMainAsync(rows.Single(r => r.Address.Text == "02-07-C").Id);
        Assert.Equal("*01-03-B *02-07-C 02-02-A 02-05-A", await Shown());

        await service.RemoveVariantRackAsync(rows.Single(r => r.Address.Text == "02-07-C").Id);   // the main one: the next becomes main
        Assert.Equal("*01-03-B *02-02-A 02-05-A", await Shown());
        await service.RemoveVariantRackAsync(rows.Single(r => r.Address.Text == "01-03-B").Id);
        Assert.Equal("*02-02-A 02-05-A", await Shown());
    }

    [Fact]
    public async Task Same_for_all_variants_gives_every_other_variant_exactly_these_racks()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = Service(db);
        await service.AddRacksAsync(Places.GodownId, 1, 3, 3);
        var racks = (await service.AllAsync()).Single(p => p.Id == Places.GodownId).Racks;
        await service.AddVariantRackAsync(seed.Variant2Id, racks[0].Id, 'A', makeMain: false);   // replaced by the copy
        await service.AddVariantRackAsync(seed.Variant1Id, racks[1].Id, 'B', makeMain: false);
        await service.AddVariantRackAsync(seed.Variant1Id, racks[2].Id, 'C', makeMain: false);

        Assert.Equal(1, await service.CopyToOtherVariantsAsync(seed.Variant1Id));

        var both = await service.RacksOfVariantsAsync([seed.Variant1Id, seed.Variant2Id]);
        string Shown(Guid v) => string.Join(" ", both[v].Select(r => (r.IsMain ? "*" : "") + r.Address));
        Assert.Equal("*02-02-B 02-03-C", Shown(seed.Variant1Id));
        Assert.Equal(Shown(seed.Variant1Id), Shown(seed.Variant2Id));
    }

    [Fact]
    public async Task Rack_changes_go_into_the_products_history_without_stamping_the_product()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var service = Service(db, "Ramesh");
        await service.AddRacksAsync(Places.GodownId, 5, 5, 2);
        var rack = (await service.AllAsync()).Single(p => p.Id == Places.GodownId).Racks.Single();
        DateTime? changedBefore;
        await using (var c = db.Open())
        {
            changedBefore = (await c.Products.FindAsync([seed.ProductId], Ct))!.ChangedAt;
        }

        await service.AddVariantRackAsync(seed.Variant1Id, rack.Id, 'A', makeMain: false);

        await using var d = db.Open();
        Assert.Equal(changedBefore, (await d.Products.FindAsync([seed.ProductId], Ct))!.ChangedAt);   // no clash with someone editing the product
        var log = await d.ActivityLog.Where(a => a.RecordType == nameof(ProductEntity) && a.Action == "Changed").ToListAsync(Ct);
        var line = Assert.Single(log);
        Assert.Equal(seed.ProductId.ToString(), line.RecordId);
        Assert.Equal("Ramesh", line.By);
        Assert.Equal("Variant V1: rack 02-05-A added (main rack in Godown)", line.Text);
        Assert.Contains(await d.ActivityLog.Select(a => a.Text).ToListAsync(Ct), t => t == "Godown: rack 05 added, levels A–B");
    }
}
