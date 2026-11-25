using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Models;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Tests;

/// <summary>
/// Version 3.0, batch 1c-2: "someone else changed this, reload". Each test plays two people in two browser tabs
/// (<see cref="TestDatabase.OpenTab"/>) on the same database.
/// </summary>
public class EditClashTests
{
    private static readonly DateOnly Day = new(2026, 11, 25);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Guid> NewCustomerAsync(TestDatabase test)
    {
        var customer = new CustomerEntity { CustomerId = "C1", Name = "One" };
        await new CustomerRepositoryEF(test.OpenTab("Setup").Factory).Create(customer);
        return customer.Id;
    }

    private static CustomerEntity Edited(CustomerEntity opened, string name) =>
        new() { Id = opened.Id, CustomerId = opened.CustomerId, Name = name, Mobiles = opened.Mobiles };

    [Fact]
    public async Task The_second_save_of_the_same_record_is_refused_and_the_first_is_kept()
    {
        using var test = new TestDatabase();
        var id = await NewCustomerAsync(test);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);

        var hitenOpened = await hiten.GetById(id);
        var adminOpened = await admin.GetById(id);
        await admin.Update(Edited(adminOpened, "Changed by Admin"));

        var refused = await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Update(Edited(hitenOpened, "Changed by Hiten")));
        Assert.StartsWith("Admin changed this customer at ", refused.Message);
        Assert.EndsWith(" while you had it open. Reload to see their changes.", refused.Message);

        await using var db = test.Open();
        Assert.Equal("Changed by Admin", (await db.Customers.SingleAsync(c => c.Id == id, Token)).Name);
    }

    [Fact]
    public async Task After_reloading_the_save_goes_through()
    {
        using var test = new TestDatabase();
        var id = await NewCustomerAsync(test);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);
        await hiten.GetById(id);
        await admin.Update(Edited(await admin.GetById(id), "Changed by Admin"));

        var reloaded = await hiten.GetById(id);
        await hiten.Update(Edited(reloaded, "Changed by Hiten"));

        await using var db = test.Open();
        Assert.Equal("Changed by Hiten", (await db.Customers.SingleAsync(c => c.Id == id, Token)).Name);
    }

    [Fact]
    public async Task Saving_several_times_from_the_same_screen_is_not_a_clash()
    {
        using var test = new TestDatabase();
        var id = await NewCustomerAsync(test);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var opened = await hiten.GetById(id);

        await hiten.Update(Edited(opened, "First"));
        await hiten.Update(Edited(opened, "Second"));
        await hiten.Update(Edited(opened, "Third"));

        await using var db = test.Open();
        Assert.Equal("Third", (await db.Customers.SingleAsync(c => c.Id == id, Token)).Name);
    }

    [Fact]
    public async Task Two_people_on_different_records_never_clash()
    {
        using var test = new TestDatabase();
        var first = await NewCustomerAsync(test);
        var second = new CustomerEntity { CustomerId = "C2", Name = "Two" };
        await new CustomerRepositoryEF(test.Factory).Create(second);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);

        var hitenOpened = await hiten.GetById(first);
        var adminOpened = await admin.GetById(second.Id);
        await admin.Update(Edited(adminOpened, "Admin's"));
        await hiten.Update(Edited(hitenOpened, "Hiten's"));

        await using var db = test.Open();
        Assert.Equal("Hiten's", (await db.Customers.SingleAsync(c => c.Id == first, Token)).Name);
        Assert.Equal("Admin's", (await db.Customers.SingleAsync(c => c.Id == second.Id, Token)).Name);
    }

    [Fact]
    public async Task A_delete_of_a_record_someone_else_changed_is_refused()
    {
        using var test = new TestDatabase();
        var id = await NewCustomerAsync(test);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);
        var hitenOpened = await hiten.GetById(id);
        await admin.Update(Edited(await admin.GetById(id), "Changed by Admin"));

        await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Delete(hitenOpened));

        await using var db = test.Open();
        Assert.True(await db.Customers.AnyAsync(c => c.Id == id, Token));
    }

    [Fact]
    public async Task Saving_a_record_someone_else_deleted_says_so_instead_of_pretending_to_save()
    {
        using var test = new TestDatabase();
        var id = await NewCustomerAsync(test);
        var hiten = new CustomerRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);
        var hitenOpened = await hiten.GetById(id);
        await admin.Delete(await admin.GetById(id));

        var refused = await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Update(Edited(hitenOpened, "Too late")));
        Assert.Equal("This customer was deleted by someone else while you had it open. Reload to see what is there now.", refused.Message);
    }

    [Fact]
    public async Task A_line_added_by_someone_else_stops_a_stale_purchase_save()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var purchaseId = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        var hitenTab = test.OpenTab("Hiten");
        var adminTab = test.OpenTab("Admin");
        var hiten = new PurchaseRepositoryEF(hitenTab.Factory);
        var hitenOpened = await hiten.GetById(purchaseId);
        await new PurchaseRepositoryEF(adminTab.Factory).GetById(purchaseId);

        await new PurchaseVariantRepositoryEF(adminTab.Factory).Create(new PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 5 });

        hitenOpened.Remarks = "Hiten's remark";
        var refused = await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Update(hitenOpened));
        Assert.StartsWith("Admin changed this purchase", refused.Message);
        // Adding a line from the stale screen is refused too: the remarks typed there could otherwise slip through later.
        await Assert.ThrowsAsync<RecordChangedException>(() => new PurchaseVariantRepositoryEF(hitenTab.Factory)
            .Create(new PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, Quantity = 1 }));

        await using var db = test.Open();
        Assert.Equal(1, await db.PurchaseVariant.CountAsync(Token));
        Assert.Null((await db.Purchases.SingleAsync(Token)).Remarks);
    }

    [Fact]
    public async Task On_one_screen_adding_lines_and_then_saving_the_purchase_is_not_a_clash()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var tab = test.OpenTab("Hiten");
        var purchases = new PurchaseRepositoryEF(tab.Factory);
        var lines = new PurchaseVariantRepositoryEF(tab.Factory);

        // A new purchase, as the Purchase screen makes it: create, add lines, then discount and remarks.
        var purchase = new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 };
        var id = await purchases.Create(purchase);
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 5 });
        await lines.Create(new PurchaseVariant { PurchaseEntityId = id, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, Quantity = 2 });
        await purchases.UpdateDiscount(id, 10);
        purchase.Id = id;
        purchase.Remarks = "Done";
        await purchases.Update(purchase);

        await using var db = test.Open();
        Assert.Equal("Done", (await db.Purchases.SingleAsync(Token)).Remarks);
    }

    [Fact]
    public async Task Editing_a_line_someone_else_deleted_says_so()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var purchaseId = await new PurchaseRepositoryEF(test.Factory).Create(new PurchaseEntity { Date = Day, SupplierEntityId = seed.SupplierId, VoucherId = 1 });
        await new PurchaseVariantRepositoryEF(test.Factory).Create(new PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 5 });
        PurchaseVariant line;
        await using (var db = test.Open())
        {
            line = await db.PurchaseVariant.AsNoTracking().SingleAsync(Token);
        }

        await new PurchaseVariantRepositoryEF(test.OpenTab("Admin").Factory).Delete(line);
        line.Quantity = 3;
        var refused = await Assert.ThrowsAsync<RecordChangedException>(() => new PurchaseVariantRepositoryEF(test.OpenTab("Hiten").Factory).Update(line));
        Assert.Equal("This line was deleted by someone else while you had it open. Reload to see what is there now.", refused.Message);
    }

    [Fact]
    public async Task Purchase_orders_table_a_row_changed_by_someone_else_is_not_overwritten()
    {
        using var test = new TestDatabase();
        var order = new PurchaseOrderEntity { Id = Guid.NewGuid(), ProductId = "P1", Quantity = 2, OrderStatus = OrderStatus.Open };
        await new PurchaseOrderRepositoryEF(test.Factory).Create(order);
        var hiten = new PurchaseOrderRepositoryEF(test.OpenTab("Hiten").Factory);
        var admin = new PurchaseOrderRepositoryEF(test.OpenTab("Admin").Factory);
        await hiten.GetAll();

        var adminRow = (await admin.GetAll()).Single();
        adminRow.Quantity = 9;
        await admin.Update(adminRow);

        // The screen re-reads the row just before saving it (GetById); the check still uses the version the table showed.
        var hitenRow = await hiten.GetById(order.Id);
        hitenRow.Quantity = 4;
        await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Update(hitenRow));
        await Assert.ThrowsAsync<RecordChangedException>(() => hiten.UpdateStatus([order.Id], OrderStatus.Completed));

        await using var db = test.Open();
        var saved = await db.PurchaseOrders.SingleAsync(Token);
        Assert.Equal(9, saved.Quantity);
        Assert.Equal(OrderStatus.Open, saved.OrderStatus);
    }

    [Fact]
    public async Task A_table_row_edit_keeps_who_created_it()
    {
        using var test = new TestDatabase();
        var tab = test.OpenTab("Hiten");
        var repo = new PurchaseOrderRepositoryEF(tab.Factory);
        var order = new PurchaseOrderEntity { Id = Guid.NewGuid(), ProductId = "P1", Quantity = 2 };
        await repo.Create(order);
        tab.User.Name = "Admin";

        // The screen builds the row afresh, without the stamps.
        await repo.Update(new PurchaseOrderEntity { Id = order.Id, ProductId = "P1", Quantity = 3, ChangedAt = order.ChangedAt });

        await using var db = test.Open();
        var saved = await db.PurchaseOrders.SingleAsync(Token);
        Assert.Equal("Hiten", saved.CreatedBy);
        Assert.NotNull(saved.CreatedAt);
        Assert.Equal("Admin", saved.ChangedBy);
    }

    [Fact]
    public async Task Users_and_roles_screens_refuse_with_the_plain_message()
    {
        using var test = new TestDatabase();
        int roleId;
        await using (var db = test.Open())
        {
            var role = new RoleEntity { Name = "Sales" };
            db.Roles.Add(role);
            await db.SaveChangesAsync(Token);
            roleId = role.Id;
        }

        var hiten = new RoleAdmin(test.OpenTab("Hiten").Factory);
        var admin = new RoleAdmin(test.OpenTab("Admin").Factory);
        await hiten.ListAsync();
        await admin.ListAsync();
        Assert.True((await admin.UpdateAsync(roleId, "Sales", [Permissions.ScreenSales])).Ok);

        var result = await hiten.UpdateAsync(roleId, "Counter", [Permissions.ScreenCustomers]);
        Assert.False(result.Ok);
        Assert.StartsWith("Admin changed this role at ", result.Message);

        await using var check = test.Open();
        Assert.Equal("Sales", (await check.Roles.SingleAsync(r => r.Id == roleId, Token)).Name);
    }

    [Fact]
    public async Task A_refused_save_tells_the_screen_so_it_can_offer_reload()
    {
        using var test = new TestDatabase();
        var id = await NewCustomerAsync(test);
        var hitenTab = test.OpenTab("Hiten");
        var hiten = new CustomerRepositoryEF(hitenTab.Factory);
        var admin = new CustomerRepositoryEF(test.OpenTab("Admin").Factory);
        RecordChangedException? told = null;
        hitenTab.Seen.Clash += clash => told = clash;

        var opened = await hiten.GetById(id);
        await admin.Update(Edited(await admin.GetById(id), "Changed by Admin"));
        await Assert.ThrowsAsync<RecordChangedException>(() => hiten.Update(Edited(opened, "x")));

        Assert.NotNull(told);
    }

    [Theory]
    [InlineData("Admin", 2026, 11, 25, "Admin changed this sale at 15:07 while you had it open. Reload to see their changes.")]
    [InlineData("Admin", 2026, 11, 24, "Admin changed this sale on 24-11-2026 at 15:07 while you had it open. Reload to see their changes.")]
    [InlineData(null, 2026, 11, 25, "Someone else changed this sale at 15:07 while you had it open. Reload to see their changes.")]
    public void The_message_says_who_and_when_in_plain_words(string? by, int year, int month, int day, string expected)
    {
        var message = RecordChangedException.Changed(typeof(SalesEntity), by, new DateTime(year, month, day, 15, 7, 30), new DateTime(2026, 11, 25, 16, 0, 0)).Message;
        Assert.Equal(expected, message);
    }
}
