using Inventory.Authentication;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>Version 3.0, batches 1c-3 and 1c-4: the activity log is written at every save, in plain words, and read per record.</summary>
public class ActivityLogTests
{
    private static readonly DateOnly Day = new(2026, 11, 25);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<List<ActivityEntry>> LogAsync(TestDatabase test)
    {
        await using var db = test.Open();
        return await db.ActivityLog.AsNoTracking().OrderBy(a => a.Id).ToListAsync(Token);
    }

    private static async Task ClearLogAsync(TestDatabase test)
    {
        await using var db = test.Open();
        db.ActivityLog.RemoveRange(await db.ActivityLog.ToListAsync(Token));
        await db.SaveChangesAsync(Token);
    }

    private static UserAccess Access(params string[] permissions) =>
        new("Viewer", true, true, false, [new RoleGrant(false, permissions.ToHashSet(), null)], DateTime.Now);

    private static async Task<(Guid PurchaseId, Seed Seed)> PurchaseWithLineAsync(TestDatabase test)
    {
        var seed = await test.SeedAsync();
        await ClearLogAsync(test);
        var id = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        await new PurchaseVariantRepositoryEF(test.Factory).Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 5, ProductRate = 40m });
        return (id, seed);
    }

    [Fact]
    public async Task A_new_customer_gets_one_created_line_with_who_and_a_readable_name()
    {
        using var test = new TestDatabase();
        test.CurrentUser.Name = "Hiten";
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await new CustomerRepositoryEF(test.Factory).Create(customer);

        var line = Assert.Single(await LogAsync(test));
        Assert.Equal(("Created", "Hiten", "Created: One (C1)", "Customer One (C1)"), (line.Action, line.By, line.Text, line.RecordLabel));
        Assert.Equal(nameof(CustomerEntity), line.RecordType);
        Assert.Equal(customer.Id.ToString(), line.RecordId);
    }

    [Fact]
    public async Task An_edit_writes_each_changed_field_before_and_after_and_nothing_for_an_unchanged_save()
    {
        using var test = new TestDatabase();
        var repo = new CustomerRepositoryEF(test.Factory);
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One", Area = "JP Nagar" };
        await repo.Create(customer);
        await ClearLogAsync(test);

        test.CurrentUser.Name = "Admin";
        await repo.Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "One Ltd", Area = "JP Nagar", Mobiles = [new Mobile { Phone = "9876543210" }] });
        await repo.Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "One Ltd", Area = "JP Nagar", Mobiles = [new Mobile { Phone = "9876543210" }] });

        var texts = (await LogAsync(test)).Select(l => l.Text).ToList();
        Assert.Equal(["Name: One → One Ltd", "Mobile added: 9876543210"], texts);
    }

    [Fact]
    public async Task Purchase_lines_are_logged_against_the_purchase_with_rates_marked_as_purchase_rates()
    {
        using var test = new TestDatabase();
        var (purchaseId, _) = await PurchaseWithLineAsync(test);

        var log = await LogAsync(test);
        Assert.Equal("Created: supplier Supplier One", log[0].Text);
        var added = log.Skip(1).ToList();
        Assert.Equal(["Line added: P1 Product One / V1, quantity 5", "Line P1 Product One / V1: Rate 40, Discount % 0"], added.Select(l => l.Text));
        Assert.All(added, l => Assert.Equal(("Purchase 14 of 25-11-2026", purchaseId.ToString()), (l.RecordLabel, l.RecordId)));
        Assert.Equal([null, ActivityLogWriter.PurchaseSide], added.Select(l => l.RateSide));
    }

    [Fact]
    public async Task A_changed_and_a_removed_line_say_what_they_were()
    {
        using var test = new TestDatabase();
        await PurchaseWithLineAsync(test);
        await ClearLogAsync(test);
        var lines = new PurchaseVariantRepositoryEF(test.Factory);
        PurchaseVariant line;
        await using (var db = test.Open())
        {
            line = await db.PurchaseVariant.AsNoTracking().SingleAsync(Token);
        }

        line.Quantity = 3;
        line.ProductRate = 42m;
        await lines.Update(line);
        await lines.Delete(line);

        var log = await LogAsync(test);
        Assert.Equal(["Line P1 Product One / V1: Quantity 5 → 3", "Line P1 Product One / V1: Rate 40 → 42", "Line removed: P1 Product One / V1, quantity 3"], log.Select(l => l.Text));
        Assert.Equal([null, ActivityLogWriter.PurchaseSide, null], log.Select(l => l.RateSide));
    }

    [Fact]
    public async Task A_sale_saved_again_with_its_lines_replaced_logs_only_the_real_difference()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        await ClearLogAsync(test);
        var sales = new SaleRepositoryEF(test.Factory);
        var saleId = await sales.Create(new SalesEntity
        {
            Date = Day, VoucherId = 3, CustomerEntityId = seed.CustomerId,
            SalesVariants = [new() { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2 }, new() { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, Quantity = 1 }],
        });
        Assert.Equal("Created: customer Customer One, 2 lines, 3 pieces", Assert.Single(await LogAsync(test)).Text);
        await ClearLogAsync(test);

        await sales.Update(new SalesEntity
        {
            Id = saleId, Date = Day, VoucherId = 3, CustomerEntityId = seed.CustomerId,
            SalesVariants = [new() { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 2 }, new() { ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, Quantity = 4 }],
        });

        var log = await LogAsync(test);
        Assert.True(log.Count == 1, string.Join(" | ", log.Select(l => l.Text)));
        var line = log[0];
        Assert.Equal("Line P1 Product One / V2: Quantity 1 → 4", line.Text);
        Assert.Equal("Sale 3 of 25-11-2026", line.RecordLabel);
    }

    [Fact]
    public async Task A_deleted_purchase_keeps_its_name_and_what_it_held()
    {
        using var test = new TestDatabase();
        var (purchaseId, _) = await PurchaseWithLineAsync(test);
        await ClearLogAsync(test);

        await new PurchaseRepositoryEF(test.Factory).Delete(new PurchaseEntity { Id = purchaseId });

        var line = Assert.Single(await LogAsync(test));
        Assert.Equal(("Deleted", "Deleted: supplier Supplier One, 1 line, 5 pieces", "Purchase 14 of 25-11-2026"), (line.Action, line.Text, line.RecordLabel));
    }

    [Fact]
    public async Task Product_rate_is_a_sale_rate_and_variants_are_logged_on_the_product()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        await ClearLogAsync(test);
        await using (var db = test.Open())
        {
            var product = await db.Products.SingleAsync(Token);
            product.Rate = 55m;
            db.Variants.Add(new VariantEntity { VariantId = "V3", Name = "Green", ProductEntityId = seed.ProductId });
            await db.SaveChangesAsync(Token);
        }

        var log = await LogAsync(test);
        Assert.Contains(log, l => l.Text == "Rate: 50 → 55" && l.RateSide == ActivityLogWriter.SaleSide);
        Assert.Contains(log, l => l.Text == "Variant added: V3 (Green)" && l.RecordLabel == "Product Product One (P1)" && l.RateSide is null);
    }

    [Fact]
    public async Task Users_and_roles_changes_are_logged_and_a_password_is_never_written()
    {
        using var test = new TestDatabase();
        int roleId, userId;
        await using (var db = test.Open())
        {
            var role = new RoleEntity { Name = "Sales" };
            var user = new UserEntity { Name = "Clerk", Password = "x" };
            db.AddRange(role, user);
            await db.SaveChangesAsync(Token);
            (roleId, userId) = (role.Id, user.Id);
        }

        await ClearLogAsync(test);
        test.CurrentUser.Name = "Admin";
        var users = new UserAdmin(test.Factory, new PasswordService());
        Assert.True((await new RoleAdmin(test.Factory).UpdateAsync(roleId, "Sales", [Permissions.ScreenSales])).Ok);
        Assert.True((await users.SetRolesAsync(userId, [new RoleAssignment(roleId, new DateTime(2026, 12, 31))])).Ok);
        Assert.True((await users.SetRolesAsync(userId, [new RoleAssignment(roleId, new DateTime(2026, 12, 31))])).Ok);
        Assert.True((await users.ResetPasswordAsync(userId, "Secret#2026")).Ok);
        Assert.True((await users.DisableAsync(userId, "Admin")).Ok);

        var log = await LogAsync(test);
        Assert.Equal(["Switch on: Open the Sales screen", "Role Sales given until 31-12-2026", "Password set", "Disabled"], log.Select(l => l.Text));
        Assert.DoesNotContain(log, l => l.Text.Contains("Secret"));
        Assert.All(log, l => Assert.Equal("Admin", l.By));
    }

    [Fact]
    public async Task An_import_writes_one_line_not_one_per_record()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Customers.AddRange(new CustomerEntity { CustomerId = "C1", Name = "One" }, new CustomerEntity { CustomerId = "C2", Name = "Two" });
            db.ActivityNote = "Imported 2 customers from customers.csv";
            await db.SaveChangesAsync(Token);
        }

        var line = Assert.Single(await LogAsync(test));
        Assert.Equal(("Import", "Imported 2 customers from customers.csv"), (line.Action, line.Text));
    }

    [Fact]
    public async Task A_refused_save_writes_nothing_to_the_log()
    {
        using var test = new TestDatabase();
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await new CustomerRepositoryEF(test.Factory).Create(customer);
        await ClearLogAsync(test);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);
        var opened = await hiten.GetById(customer.Id);
        await admin.Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "By Admin" });

        await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Update(new CustomerEntity { Id = opened.Id, CustomerId = "C1", Name = "By Hiten" }));

        Assert.Equal(["Name: One → By Admin"], (await LogAsync(test)).Select(l => l.Text));
    }

    [Fact]
    public async Task Logins_and_other_actions_are_written_with_the_person()
    {
        using var test = new TestDatabase();
        var service = new ActivityLogService(test.Factory, new CurrentUser { Name = "Admin" });

        await service.WriteAsync(ActivityLogService.Login, "Logged in", "Hiten");
        await service.WriteAsync(ActivityLogService.Backup, "Backup deleted: backup_1.db");

        var log = await LogAsync(test);
        Assert.Equal([("Login", "Hiten", "Logged in"), ("Backup", "Admin", "Backup deleted: backup_1.db")], log.Select(l => (l.Action, l.By, l.Text)));
    }

    [Fact]
    public async Task The_panel_shows_a_records_history_newest_first_and_hides_rates_the_person_may_not_see()
    {
        using var test = new TestDatabase();
        var (purchaseId, _) = await PurchaseWithLineAsync(test);
        var service = new ActivityLogService(test.Factory, new CurrentUser());

        var full = await service.ForRecordAsync(nameof(PurchaseEntity), purchaseId.ToString().ToUpperInvariant(), Access(Permissions.ActivitySee, Permissions.RatesPurchaseSee));
        Assert.Equal(2, full.Groups.Count);
        Assert.Equal(["Line added: P1 Product One / V1, quantity 5", "Line P1 Product One / V1: Rate 40, Discount % 0"], full.Groups[0].Lines);
        Assert.Equal(["Created: supplier Supplier One"], full.Groups[1].Lines);
        Assert.False(full.EarlierNotRecorded);

        var noRates = await service.ForRecordAsync(nameof(PurchaseEntity), purchaseId.ToString(), Access(Permissions.ActivitySee));
        Assert.Equal(["Line added: P1 Product One / V1, quantity 5"], noRates.Groups[0].Lines);

        var noSwitch = await service.ForRecordAsync(nameof(PurchaseEntity), purchaseId.ToString(), Access(Permissions.RatesPurchaseSee));
        Assert.Empty(noSwitch.Groups);
    }

    [Fact]
    public async Task A_record_from_before_version_3_says_its_earlier_history_was_not_recorded()
    {
        using var test = new TestDatabase();
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await new CustomerRepositoryEF(test.Factory).Create(customer);
        await ClearLogAsync(test);   // as if it was made by version 2.0
        await new CustomerRepositoryEF(test.Factory).Update(new CustomerEntity { Id = customer.Id, CustomerId = "C1", Name = "One Ltd" });

        var history = await new ActivityLogService(test.Factory, new CurrentUser()).ForRecordAsync(nameof(CustomerEntity), customer.Id.ToString(), Access(Permissions.ActivitySee));

        Assert.True(history.EarlierNotRecorded);
        Assert.Equal(["Name: One → One Ltd"], Assert.Single(history.Groups).Lines);
    }

    // ---- Owner, 25 Nov 2026: lines added while creating are folded into "Created"; later changes in full. ----

    private static PurchaseVariant NewLine(Guid purchaseId, Seed seed, Guid variant, int quantity) =>
        new() { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = variant, Quantity = quantity, ProductRate = 10m };

    [Fact]
    public async Task Lines_added_while_creating_a_purchase_fold_into_one_created_entry()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        await ClearLogAsync(test);
        var tab = test.OpenTab("Hiten");
        var id = await new PurchaseRepositoryEF(tab.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var lines = new PurchaseVariantRepositoryEF(tab.Factory);
        await lines.Create(NewLine(id, seed, seed.Variant1Id, 5));
        await lines.Create(NewLine(id, seed, seed.Variant2Id, 2));

        var history = await new ActivityLogService(test.Factory, new CurrentUser()).ForRecordAsync(nameof(PurchaseEntity), id.ToString(), Access(Permissions.ActivitySee, Permissions.RatesPurchaseSee));

        var created = Assert.Single(history.Groups);
        Assert.Equal(["Created: supplier Supplier One", "2 lines added while creating, 7 pieces"], created.Lines);
        Assert.Equal(4, created.Details.Count);   // two lines and their rates, behind "show lines"
        Assert.Contains("Line added: P1 Product One / V2, quantity 2", created.Details);
    }

    [Fact]
    public async Task After_leaving_the_screen_an_added_line_is_a_change_shown_in_full()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var tab = test.OpenTab("Hiten");
        var id = await new PurchaseRepositoryEF(tab.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var lines = new PurchaseVariantRepositoryEF(tab.Factory);
        await lines.Create(NewLine(id, seed, seed.Variant1Id, 5));
        tab.Seen.EndCreating();   // another screen was opened
        await lines.Create(NewLine(id, seed, seed.Variant2Id, 2));

        var history = await new ActivityLogService(test.Factory, new CurrentUser()).ForRecordAsync(nameof(PurchaseEntity), id.ToString(), Access(Permissions.ActivitySee));

        Assert.Equal(2, history.Groups.Count);
        Assert.Equal(["Line added: P1 Product One / V2, quantity 2"], history.Groups[0].Lines);
        Assert.Empty(history.Groups[0].Details);
        Assert.Equal(["Created: supplier Supplier One", "1 line added while creating, 5 pieces"], history.Groups[1].Lines);
    }

    [Fact]
    public async Task Many_lines_added_later_in_one_go_are_folded_and_a_few_are_not()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var id = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var lines = new PurchaseVariantRepositoryEF(test.Factory);
        // Bulk Entry: four lines in one save (more than ActivityLogService.FoldAbove).
        await lines.CreateRange([.. Enumerable.Range(1, 4).Select(i => NewLine(id, seed, i % 2 == 0 ? seed.Variant1Id : seed.Variant2Id, i))]);
        await lines.CreateRange([NewLine(id, seed, seed.Variant1Id, 9)]);

        var history = await new ActivityLogService(test.Factory, new CurrentUser()).ForRecordAsync(nameof(PurchaseEntity), id.ToString(), Access(Permissions.ActivitySee));

        Assert.Equal(["Line added: P1 Product One / V1, quantity 9"], history.Groups[0].Lines);
        Assert.Equal(["4 lines added, 10 pieces"], history.Groups[1].Lines);
        Assert.Equal(4, history.Groups[1].Details.Count);   // no rates: this person may not see purchase rates
    }

    // ---- Owner, 25 Nov 2026: one person's changes to one record in one sitting are one entry. ----

    [Fact]
    public async Task Lines_added_one_by_one_in_one_sitting_are_one_folded_entry()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var id = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var tab = test.OpenTab("Hiten");
        await new PurchaseRepositoryEF(tab.Factory).GetById(id);   // the purchase is opened again later
        var lines = new PurchaseVariantRepositoryEF(tab.Factory);
        foreach (var quantity in new[] { 1, 2, 3, 4 })
        {
            await lines.Create(NewLine(id, seed, quantity % 2 == 0 ? seed.Variant1Id : seed.Variant2Id, quantity));
        }

        var history = await new ActivityLogService(test.Factory, new CurrentUser()).ForRecordAsync(nameof(PurchaseEntity), id.ToString(), Access(Permissions.ActivitySee));

        Assert.Equal(2, history.Groups.Count);
        Assert.Equal(["4 lines added, 10 pieces"], history.Groups[0].Lines);
        Assert.Equal(4, history.Groups[0].Details.Count);
        Assert.Equal("Hiten", history.Groups[0].By);
    }

    [Fact]
    public async Task Leaving_the_screen_starts_a_new_entry()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var id = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var tab = test.OpenTab("Hiten");
        var lines = new PurchaseVariantRepositoryEF(tab.Factory);
        await lines.Create(NewLine(id, seed, seed.Variant1Id, 1));
        tab.Seen.EndCreating();   // another screen, then back
        await lines.Create(NewLine(id, seed, seed.Variant2Id, 2));

        var history = await new ActivityLogService(test.Factory, new CurrentUser()).ForRecordAsync(nameof(PurchaseEntity), id.ToString(), Access(Permissions.ActivitySee));

        Assert.Equal(3, history.Groups.Count);
    }

    [Fact]
    public void A_pause_longer_than_half_an_hour_starts_a_new_sitting()
    {
        var seen = new SeenVersions();
        var start = new DateTime(2026, 11, 25, 12, 0, 0);
        var first = seen.SittingFor("PurchaseEntity:1", start);

        Assert.Equal(first, seen.SittingFor("PurchaseEntity:1", start.AddMinutes(25)));
        Assert.Equal(first, seen.SittingFor("PurchaseEntity:1", start.AddMinutes(50)));   // 25 minutes after the last change
        Assert.NotEqual(first, seen.SittingFor("PurchaseEntity:1", start.AddMinutes(81)));
        Assert.NotEqual(first, seen.SittingFor("PurchaseEntity:2", start.AddMinutes(81)));   // another record
    }

    [Fact]
    public void A_sitting_shows_when_it_began_and_ended()
    {
        var at = new DateTime(2026, 11, 25, 12, 30, 0);
        Assert.Equal("25-11-2026 12:30–12:45", new ActivityGroup(at, "Hiten", [], [], at.AddMinutes(15)).When);
        Assert.Equal("25-11-2026 12:30", new ActivityGroup(at, "Hiten", [], [], at.AddSeconds(20)).When);
    }

    [Fact]
    public async Task An_empty_discount_becoming_0_is_not_a_change()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var repo = new PurchaseRepositoryEF(test.Factory);
        var purchase = new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 };
        purchase.Id = await repo.Create(purchase);
        await ClearLogAsync(test);

        purchase.DiscountAmount = 0m;
        purchase.Remarks = "checked";
        await repo.Update(purchase);

        Assert.Equal(["Remarks: (blank) → checked"], (await LogAsync(test)).Select(l => l.Text));
    }

    // ---- Batch 1c-5: the Activity log screen. ----

    private static ActivityFilter Week(string? person = null, ActivityKind kind = ActivityKind.Everything, string? search = null) =>
        new(DateOnly.FromDateTime(DateTime.Today.AddDays(-6)), DateOnly.FromDateTime(DateTime.Today), person, kind, search);

    private static UserAccess AdminAccess() =>
        new("Admin", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now);

    [Fact]
    public async Task The_screen_lists_sittings_newest_first_with_filters_for_person_kind_and_words()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        await ClearLogAsync(test);
        test.CurrentUser.Name = "Hiten";
        await new CustomerRepositoryEF(test.Factory).Update(new CustomerEntity { Id = seed.CustomerId, CustomerId = "C1", Name = "Customer One", Area = "JP Nagar" });
        test.CurrentUser.Name = "Admin";
        var purchaseId = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var service = new ActivityLogService(test.Factory, new CurrentUser { Name = "Admin" });
        await service.WriteAsync(ActivityLogService.Login, "Logged in", "Hiten");

        var all = await service.SearchAsync(Week(), AdminAccess());
        Assert.Equal(["Logged in", "Created: supplier Supplier One", "Area: (blank) → JP Nagar"], all.Select(r => r.Group.Lines[0]));
        Assert.Equal(["system", "purchase", "party"], all.Select(r => r.Kind));
        Assert.Equal($"/purchase/{purchaseId}", all[1].Link);

        Assert.Equal(2, (await service.SearchAsync(Week(person: "hiten"), AdminAccess())).Count);
        Assert.Single(await service.SearchAsync(Week(kind: ActivityKind.Logins), AdminAccess()));
        Assert.Single(await service.SearchAsync(Week(kind: ActivityKind.Purchases), AdminAccess()));
        Assert.Equal("Customer Customer One (C1)", Assert.Single(await service.SearchAsync(Week(search: "jp nag"), AdminAccess())).RecordLabel);
        Assert.Empty(await service.SearchAsync(new ActivityFilter(Day.AddYears(-1), Day.AddYears(-1), null, ActivityKind.Everything, null), AdminAccess()));
    }

    [Fact]
    public async Task The_screen_hides_rates_and_cannot_find_them_by_searching()
    {
        using var test = new TestDatabase();
        await PurchaseWithLineAsync(test);
        var service = new ActivityLogService(test.Factory, new CurrentUser());

        var noRates = await service.SearchAsync(Week(search: "Rate 40"), Access(Permissions.ActivitySee));
        var withRates = await service.SearchAsync(Week(search: "Rate 40"), Access(Permissions.ActivitySee, Permissions.RatesPurchaseSee));

        Assert.Empty(noRates);
        Assert.Single(withRates);
        Assert.Empty(await service.SearchAsync(Week(), Access(Permissions.ScreenPurchases)));   // no "See activity log" switch
    }

    [Fact]
    public async Task A_deleted_record_keeps_its_name_but_has_no_link()
    {
        using var test = new TestDatabase();
        var (purchaseId, _) = await PurchaseWithLineAsync(test);
        await new PurchaseRepositoryEF(test.Factory).Delete(new PurchaseEntity { Id = purchaseId });

        var rows = await new ActivityLogService(test.Factory, new CurrentUser()).SearchAsync(Week(), AdminAccess());

        Assert.All(rows, r => Assert.Null(r.Link));
        Assert.All(rows, r => Assert.Equal("Purchase 14 of 25-11-2026", r.RecordLabel));
    }

    [Fact]
    public async Task Only_the_Admin_role_may_erase_and_the_erase_leaves_no_trace()
    {
        using var test = new TestDatabase();
        await PurchaseWithLineAsync(test);
        var service = new ActivityLogService(test.Factory, new CurrentUser { Name = "Admin" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EraseAllAsync(Access(Permissions.ActivitySee)));
        Assert.Equal(3, await service.CountAsync());

        Assert.Equal(3, await service.EraseAllAsync(AdminAccess()));
        Assert.Equal(0, await service.CountAsync());
    }

    [Fact]
    public void Each_person_keeps_one_tint_and_none_is_red()
    {
        Assert.Equal(ActivityLogService.PersonHue("Hiten"), ActivityLogService.PersonHue(" hiten "));
        foreach (var name in new[] { "Hiten", "Admin", "Clerk", "UiLogViewer", "Ravi", "Sita", "Counter 2" })
        {
            var hue = ActivityLogService.PersonHue(name);
            Assert.InRange(hue, 30, 330);   // red sits around 0 / 360
        }
    }

    [Fact]
    public async Task The_first_ten_users_all_get_different_tints_in_the_order_they_were_added()
    {
        using var test = new TestDatabase();
        await using (var db = test.Open())
        {
            db.Users.AddRange(Enumerable.Range(1, 10).Select(i => new UserEntity { Name = $"User{i}", Password = "x" }));
            await db.SaveChangesAsync(Token);
        }

        var hues = await new ActivityLogService(test.Factory, new CurrentUser()).PersonHuesAsync();

        Assert.Equal(10, hues.Values.Distinct().Count());
        Assert.Equal(hues["User1"], hues["user1"]);
        Assert.All(hues.Values, h => Assert.InRange(h, 30, 330));
    }
}
