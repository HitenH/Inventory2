using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Batch 14: Reports → Sales &amp; Purchases periods mean what their labels say (financial year April–March).</summary>
public class ReportPeriodTests
{
    private static readonly DateOnly Wed12Nov2026 = new(2026, 11, 12);

    private static (DateOnly, DateOnly) R(string key, DateOnly today) => ReportPeriods.Range(key, today);
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Each_period_covers_the_dates_its_label_says()
    {
        var today = Wed12Nov2026;
        Assert.Equal((D(2026, 11, 12), D(2026, 11, 12)), R(ReportPeriods.Today, today));
        Assert.Equal((D(2026, 11, 11), D(2026, 11, 11)), R(ReportPeriods.Yesterday, today));
        Assert.Equal((D(2026, 11, 9), today), R(ReportPeriods.ThisWeek, today));       // Monday 9 Nov
        Assert.Equal((D(2026, 11, 6), today), R(ReportPeriods.Last7Days, today));      // 7 days including today
        Assert.Equal((D(2026, 11, 1), today), R(ReportPeriods.ThisMonth, today));
        Assert.Equal((D(2026, 10, 12), today), R(ReportPeriods.Last1Month, today));
        Assert.Equal((D(2026, 7, 1), D(2026, 9, 30)), R(ReportPeriods.LastQuarter, today));
        Assert.Equal((D(2026, 8, 12), today), R(ReportPeriods.Last3Months, today));
        Assert.Equal((D(2026, 5, 12), today), R(ReportPeriods.Last6Months, today));
        Assert.Equal((D(2026, 4, 1), today), R(ReportPeriods.ThisYear, today));        // financial year
        Assert.Equal((D(2025, 11, 12), today), R(ReportPeriods.Last12Months, today));
    }

    [Theory]
    [InlineData(2027, 2, 15, 2026)]   // Jan–Mar belongs to the financial year that started the April before
    [InlineData(2026, 4, 1, 2026)]
    [InlineData(2026, 3, 31, 2025)]
    public void Financial_year_starts_on_1_April(int y, int m, int d, int startYear)
        => Assert.Equal(D(startYear, 4, 1), ReportPeriods.FinancialYearStart(D(y, m, d)));

    [Fact]
    public void Edge_days_week_starting_Sunday_and_last_quarter_in_January()
    {
        Assert.Equal((D(2026, 11, 9), D(2026, 11, 15)), R(ReportPeriods.ThisWeek, D(2026, 11, 15)));   // Sunday
        Assert.Equal((D(2026, 11, 16), D(2026, 11, 16)), R(ReportPeriods.ThisWeek, D(2026, 11, 16))); // Monday
        Assert.Equal((D(2025, 10, 1), D(2025, 12, 31)), R(ReportPeriods.LastQuarter, D(2026, 1, 20)));
        Assert.Equal((D(2026, 1, 1), D(2026, 3, 31)), R(ReportPeriods.LastQuarter, D(2026, 4, 1)));
    }

    [Fact]
    public void Every_period_on_screen_has_a_date_range()
    {
        foreach (var period in ReportPeriods.All)
        {
            var (from, to) = ReportPeriods.Range(period.Key, Wed12Nov2026);
            Assert.True(from <= to, period.Key);
            Assert.True(to <= Wed12Nov2026, period.Key);
        }
    }

    [Fact]
    public async Task Totals_for_a_period_are_added_up_in_the_database_including_both_end_days()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        await using (var c = db.Open())
        {
            c.Sales.AddRange(
                new SalesEntity { Date = D(2026, 3, 31), VoucherId = 1, CustomerEntityId = seed.CustomerId, TotalAmountProduct = 1000m },
                new SalesEntity { Date = D(2026, 4, 1), VoucherId = 1, CustomerEntityId = seed.CustomerId, TotalAmountProduct = 10.5m },
                new SalesEntity { Date = D(2026, 11, 12), VoucherId = 1, CustomerEntityId = seed.CustomerId, TotalAmountProduct = 20m },
                new SalesEntity { Date = D(2026, 11, 13), VoucherId = 1, CustomerEntityId = seed.CustomerId, TotalAmountProduct = 5000m });
            c.Purchases.AddRange(
                new PurchaseEntity { Date = D(2026, 4, 1), VoucherId = 1, SupplierEntityId = seed.SupplierId, TotalAmountProduct = 7m },
                new PurchaseEntity { Date = D(2026, 6, 1), VoucherId = 1, SupplierEntityId = seed.SupplierId, TotalAmountProduct = null });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (from, to) = ReportPeriods.Range(ReportPeriods.ThisYear, Wed12Nov2026);
        Assert.Equal(30.5m, await new SaleRepositoryEF(db.Factory).GetTotalBetweenAsync(from, to));
        Assert.Equal(7m, await new PurchaseRepositoryEF(db.Factory).GetTotalBetweenAsync(from, to));
        Assert.Equal(0m, await new SaleRepositoryEF(db.Factory).GetTotalBetweenAsync(D(2020, 1, 1), D(2020, 1, 2)));
    }
}
