using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Version 3.0, stage L, batch L-1: the label rule book (Service/Labels.cs).</summary>
public class LabelsTests
{
    private static readonly LabelSheetLayout Sheet = LabelSheetLayout.A4Sheet65;

    private static LabelItem Item(int copies, string product = "AJ0120103", string variant = "G") =>
        new(product, "1 NO HAATICHAP (NS/11)", variant, "GANESH", copies);

    [Fact]
    public void The_QR_holds_product_ID_Tab_variant_ID_as_the_scanner_expects()
    {
        Assert.Equal("AJ0120103\tG", Labels.QrText("AJ0120103", "G"));
        Assert.Equal("AJ0120103\tG", Labels.QrText(" AJ0120103 ", " G "));
    }

    [Fact]
    public void The_sheet_matches_the_Word_file_and_fits_on_A4()
    {
        Assert.Equal(65, Sheet.PerSheet);
        Assert.Equal((40, 21), (Sheet.LabelWidthMm, Sheet.LabelHeightMm));
        Assert.Equal(4, Sheet.LeftOf(0));
        Assert.Equal(164, Sheet.LeftOf(4));
        Assert.Equal(12, Sheet.TopOf(0));
        Assert.Equal(264, Sheet.TopOf(12));
        Assert.True(Sheet.LeftOf(Sheet.Columns) <= Sheet.PageWidthMm);
        Assert.True(Sheet.TopOf(Sheet.Rows) <= Sheet.PageHeightMm);
    }

    [Fact]
    public void Labels_fill_each_sheet_from_the_bottom_right_backwards_then_the_next_sheet()
    {
        var spots = Labels.Arrange([Item(60), Item(10, variant: "L")], Sheet);

        Assert.Equal(70, spots.Count);
        Assert.Equal((0, 12, 4), (spots[0].Sheet, spots[0].Row, spots[0].Column));   // bottom-right first
        Assert.Equal((0, 12, 3), (spots[1].Sheet, spots[1].Row, spots[1].Column));   // then to its left
        Assert.Equal((0, 11, 4), (spots[5].Sheet, spots[5].Row, spots[5].Column));   // then the row above
        Assert.Equal((0, 0, 0), (spots[64].Sheet, spots[64].Row, spots[64].Column)); // top-left last
        Assert.Equal((1, 12, 4), (spots[65].Sheet, spots[65].Row, spots[65].Column));
        Assert.Equal("L", spots[60].Item.VariantId);
        Assert.Equal(2, Labels.SheetsFor(70, Sheet));
        Assert.Equal(1, Labels.SheetsFor(65, Sheet));
        Assert.Equal(0, Labels.SheetsFor(0, Sheet));
    }

    [Fact]
    public void A_part_used_sheet_starts_just_above_the_used_labels()
    {
        var spots = Labels.Arrange([Item(52)], Sheet, usedOnFirstSheet: 15);

        Assert.Equal((0, 9, 4), (spots[0].Sheet, spots[0].Row, spots[0].Column));    // 15 used = the bottom three rows
        Assert.Equal((0, 0, 0), (spots[49].Sheet, spots[49].Row, spots[49].Column)); // the first sheet holds 50
        Assert.Equal((1, 12, 4), (spots[50].Sheet, spots[50].Row, spots[50].Column));
        Assert.Equal(2, Labels.SheetsFor(52, Sheet, 15));
        Assert.Equal(1, Labels.SheetsFor(50, Sheet, 15));
        Assert.Equal(15, Labels.UsedSpots(15, Sheet).Count);
        Assert.Contains((12, 0), Labels.UsedSpots(15, Sheet));
        Assert.DoesNotContain((9, 4), Labels.UsedSpots(15, Sheet));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public void Already_used_must_leave_at_least_one_label(int used)
    {
        Assert.Equal("Already used must be a whole number from 0 to 64.", Labels.CheckUsed(used, Sheet));
        Assert.Null(Labels.CheckUsed(64, Sheet));
    }

    [Fact]
    public void Everything_inside_a_label_moves_down_and_left_a_little_after_the_printer_tests()
    {
        Assert.Equal(2.2, Sheet.ContentDownMm);
        Assert.Equal(0.5, Sheet.ContentLeftMm);
    }

    [Theory]
    [InlineData(0, 10, 10)]    // a fresh sheet, 10 printed
    [InlineData(10, 20, 30)]   // then 20 more
    [InlineData(30, 40, 5)]    // then 40: the first sheet is finished (35) and 5 of the next are used
    [InlineData(60, 5, 0)]     // a sheet used up exactly: the next print starts on a fresh one
    [InlineData(0, 130, 0)]
    public void The_page_remembers_where_the_last_print_ended(int used, int printed, int next)
    {
        Assert.Equal(next, Labels.UsedAfterPrint(used, printed, Sheet));
    }

    [Theory]
    [InlineData(0, 0, "Copies must be a whole number from 1 to 650.")]
    [InlineData(651, 0, "Copies must be a whole number from 1 to 650.")]
    [InlineData(10, 1295, "At most 1300 labels can be printed in one go.")]
    public void Too_few_or_too_many_copies_are_refused_with_a_plain_message(int copies, int listed, string message)
    {
        Assert.Equal(message, Labels.Check(Item(copies), listed));
    }

    [Fact]
    public void A_line_needs_a_product_and_variant_and_no_Tab_inside_them()
    {
        Assert.Null(Labels.Check(Item(5), 0));
        Assert.Equal("Choose a product and a variant first.", Labels.Check(Item(5, variant: " "), 0));
        Assert.Contains("Tab", Labels.Check(Item(5, product: "AJ\t01"), 0));
    }

    [Fact]
    public void The_QR_picture_is_made_here_sharp_and_the_same_every_time()
    {
        var svg = Labels.QrSvg(Labels.QrText("AJ0120103", "G"));

        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ", svg);
        Assert.Contains("shape-rendering=\"crispEdges\"", svg);
        Assert.Contains("<path d=\"M", svg);
        Assert.Equal(svg, Labels.QrSvg("AJ0120103\tG"));
        Assert.NotEqual(svg, Labels.QrSvg("AJ0120103\tB"));
    }

    [Fact]
    public void Millimetres_are_written_with_a_dot_whatever_the_PC_language()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("12.5mm", Labels.Mm(12.5));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    // ---- Owner, 25 Nov 2026: named label set-ups instead of numbers fixed in the program. ----

    [Fact]
    public void Gaps_between_labels_move_the_next_label_along()
    {
        var gapped = Sheet with { GapAcrossMm = 2, GapDownMm = 1 };
        Assert.Equal(4 + 2 * 42, gapped.LeftOf(2));
        Assert.Equal(12 + 3 * 22, gapped.TopOf(3));
    }

    [Fact]
    public void Numbers_that_do_not_fit_the_page_are_refused_in_plain_words()
    {
        Assert.Empty(Sheet.Problems());
        Assert.Contains("The labels reach 214 mm across, but the page is 210 mm wide.", (Sheet with { LeftMm = 14 }).Problems());
        Assert.Contains("The QR code (25 mm) is bigger than the label.", (Sheet with { QrMm = 25 }).Problems());
        Assert.Contains("Give the label a name.", (Sheet with { Name = " " }).Problems());
    }

    private static UserAccess Admin() => new("Admin", true, true, false, [new RoleGrant(true, new HashSet<string>(), null)], DateTime.Now);
    private static UserAccess Printer() => new("Clerk", true, true, false, [new RoleGrant(false, new HashSet<string> { Permissions.LabelsPrint }, null)], DateTime.Now);

    [Fact]
    public async Task The_upgrade_brings_A4_65_Label_in_use()
    {
        using var test = new TestDatabase();
        var active = await new LabelLayoutService(test.Factory).ActiveAsync();
        Assert.Equal(("A4 65 Label", true, 65, 2.2), (active.Name, active.IsActive, active.PerSheet, active.ContentDownMm));
    }

    [Fact]
    public async Task Only_the_settings_switch_may_add_a_label_and_names_are_unique()
    {
        using var test = new TestDatabase();
        var service = new LabelLayoutService(test.Factory);
        var sticker = Sheet with { Name = "Sticker 50x25", PageWidthMm = 50, PageHeightMm = 25, Columns = 1, Rows = 1, LabelWidthMm = 50, LabelHeightMm = 25, TopMm = 0, LeftMm = 0 };

        Assert.False((await service.SaveAsync(sticker, Printer())).Ok);
        Assert.True((await service.SaveAsync(sticker, Admin())).Ok);
        Assert.Equal("There is already a label called a4 65 label.", (await service.SaveAsync(Sheet with { Name = "a4 65 label" }, Admin())).Message);
        Assert.Equal(2, (await service.ListAsync()).Count);
    }

    [Fact]
    public async Task Anyone_who_prints_may_switch_labels_but_the_one_in_use_cannot_be_deleted()
    {
        using var test = new TestDatabase();
        var service = new LabelLayoutService(test.Factory);
        await service.SaveAsync(Sheet with { Name = "Second" }, Admin());
        var second = (await service.ListAsync()).Single(l => l.Name == "Second");

        Assert.True((await service.UseAsync(second.Id, Printer())).Ok);
        Assert.Equal("Second", (await service.ActiveAsync()).Name);
        Assert.Equal("This label is in use. Choose another label first.", (await service.DeleteAsync(second.Id, Admin())).Message);
        Assert.True((await service.DeleteAsync(1, Admin())).Ok);
        Assert.Single(await service.ListAsync());   // the last one left is always the one in use, so it stays
    }

    // ---- Batch L-2 (Hiten, 25 Nov 2026): labels from a purchase, menu, shortcuts, used count, print log. ----

    [Fact]
    public void Alt_L_opens_Labels_and_on_a_saved_purchase_fills_it_with_that_purchase()
    {
        var id = Guid.NewGuid();
        Assert.Equal("/labels", Shortcuts.Target("/products", 'L', false));
        Assert.Equal($"/labels?purchase={id}", Shortcuts.Target($"/purchase/{id}", 'L', false));
        Assert.Equal("/labels", Shortcuts.Target("/purchase", 'L', false));   // a new, unsaved purchase
        Assert.Equal("/labels", Shortcuts.Back("/labelsettings"));
    }

    [Fact]
    public void Esc_on_Labels_goes_back_to_the_purchase_or_product_it_was_opened_from()
    {
        var purchase = Guid.NewGuid();
        var product = Guid.NewGuid();
        var variant = Guid.NewGuid();
        Assert.Equal($"/purchase/{purchase}", Shortcuts.Back($"/labels?purchase={purchase}"));
        Assert.Equal($"/product/{product}", Shortcuts.Back($"/labels?product={product}&variant={variant}"));
        Assert.Equal("/", Shortcuts.Back("/labels"));                  // opened from the menu: Dashboard, as before
        Assert.Equal("/", Shortcuts.Back("/labels?purchase=rubbish"));
    }

    [Fact]
    public async Task A_purchase_gives_one_line_per_purchase_line_as_entered_and_leaves_out_minus_lines()
    {
        using var test = new TestDatabase();
        var seed = await test.SeedAsync();
        var purchaseId = await new Inventory.Domain.Repository.PurchaseRepositoryEF(test.Factory).Create(new Inventory.Domain.Entities.PurchaseEntity { Date = new DateOnly(2026, 11, 25), SupplierEntityId = seed.SupplierId, VoucherId = 14 });
        var lines = new Inventory.Domain.Repository.PurchaseVariantRepositoryEF(test.Factory);
        await lines.Create(new Inventory.Domain.Entities.PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 10, SerialNumber = 1 });
        await lines.Create(new Inventory.Domain.Entities.PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant2Id, Quantity = -2, SerialNumber = 2 });
        await lines.Create(new Inventory.Domain.Entities.PurchaseVariant { PurchaseEntityId = purchaseId, ProductEntityId = seed.ProductId, VariantEntityId = seed.Variant1Id, Quantity = 5, SerialNumber = 3 });

        var labels = await new LabelSourceService(test.Factory).ForPurchaseAsync(purchaseId);

        Assert.NotNull(labels);
        Assert.Equal("Purchase 14 of 25-11-2026", labels.Label);
        Assert.Equal([("P1", "V1", 10), ("P1", "V1", 5)], labels.Items.Select(i => (i.ProductId, i.VariantId, i.Copies)));
        Assert.Equal(("Product One", "Red"), (labels.Items[0].ProductName, labels.Items[0].VariantName));
        Assert.Null(await new LabelSourceService(test.Factory).ForPurchaseAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Already_used_is_kept_in_the_database_per_label_set_up()
    {
        using var test = new TestDatabase();
        var sources = new LabelSourceService(test.Factory);
        Assert.Equal(0, await sources.UsedAsync(1));

        await sources.SetUsedAsync(1, 30);
        await sources.SetUsedAsync(1, 5);

        Assert.Equal(5, await new LabelSourceService(test.Factory).UsedAsync(1));   // as after a restart
        Assert.Equal(0, await sources.UsedAsync(2));
    }

    [Fact]
    public void A_print_is_written_down_with_what_and_how_many()
    {
        Assert.Equal("Printed 15 labels on A4 65 Label: AJ0120103/G ×10, AJ0320105/2T ×5",
            Labels.PrintedText([Item(10), Item(5, "AJ0320105", "2T")], "A4 65 Label"));
        Assert.EndsWith(" and 2 more lines", Labels.PrintedText([Item(1), Item(1), Item(1)], "A4 65 Label", listAtMost: 1));
    }

    [Fact]
    public async Task A_print_from_a_purchase_shows_in_that_purchases_activity_panel()
    {
        using var test = new TestDatabase();
        var purchaseId = Guid.NewGuid();
        var service = new ActivityLogService(test.Factory, new Inventory.Domain.CurrentUser { Name = "Hiten" });

        await service.TryWriteLabelsAsync("Printed 10 labels on A4 65 Label: P1/V1 ×10", new PurchaseLabels(purchaseId, "Purchase 14 of 25-11-2026", []), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var history = await service.ForRecordAsync(nameof(Inventory.Domain.Entities.PurchaseEntity), purchaseId.ToString(), Admin());
        Assert.Equal(["Printed 10 labels on A4 65 Label: P1/V1 ×10"], Assert.Single(history.Groups).Lines);
        Assert.Equal("Hiten", history.Groups[0].By);
    }
}
