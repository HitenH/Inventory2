namespace Inventory.Service;

/// <summary>
/// The periods offered on Reports → Sales &amp; Purchases (batch 14, Hiten's choice: rolling and calendar
/// periods side by side, years and quarters on the financial year April–March).
/// Every period ends today (inclusive); the old screen also counted vouchers dated in the future.
/// </summary>
public static class ReportPeriods
{
    public sealed record Period(string Key, string Label);

    public const string Today = "Today";
    public const string Yesterday = "Yesterday";
    public const string ThisWeek = "ThisWeek";
    public const string Last7Days = "Last7Days";
    public const string ThisMonth = "ThisMonth";
    public const string Last1Month = "Last1Month";
    public const string LastQuarter = "LastQuarter";
    public const string Last3Months = "Last3Months";
    public const string Last6Months = "Last6Months";
    public const string ThisYear = "ThisYear";
    public const string Last12Months = "Last12Months";

    /// <summary>Financial year starts on 1 April.</summary>
    public const int FinancialYearStartMonth = 4;

    public static readonly IReadOnlyList<Period> All = new[]
    {
        new Period(Today, "Today"),
        new Period(Yesterday, "Yesterday"),
        new Period(ThisWeek, "This week (Monday to today)"),
        new Period(Last7Days, "Last 7 days"),
        new Period(ThisMonth, "This month (1st to today)"),
        new Period(Last1Month, "Last 1 month"),
        new Period(LastQuarter, "Last quarter (previous full quarter)"),
        new Period(Last3Months, "Last 3 months"),
        new Period(Last6Months, "Last 6 months"),
        new Period(ThisYear, "This financial year (1 April to today)"),
        new Period(Last12Months, "Last 12 months"),
    };

    /// <summary>First and last day (both included) of a period, seen from <paramref name="today"/>.</summary>
    public static (DateOnly From, DateOnly To) Range(string key, DateOnly today) => key switch
    {
        Today => (today, today),
        Yesterday => (today.AddDays(-1), today.AddDays(-1)),
        ThisWeek => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today),   // Monday = start of the week
        Last7Days => (today.AddDays(-6), today),
        ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
        Last1Month => (today.AddMonths(-1), today),
        LastQuarter => PreviousQuarter(today),
        Last3Months => (today.AddMonths(-3), today),
        Last6Months => (today.AddMonths(-6), today),
        ThisYear => (FinancialYearStart(today), today),
        Last12Months => (today.AddYears(-1), today),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown report period"),
    };

    public static DateOnly FinancialYearStart(DateOnly today)
        => new(today.Month >= FinancialYearStartMonth ? today.Year : today.Year - 1, FinancialYearStartMonth, 1);

    /// <summary>
    /// The last complete quarter. Financial quarters (Apr–Jun, Jul–Sep, Oct–Dec, Jan–Mar) start on the same
    /// months as calendar quarters, so this is simply the previous three-month block.
    /// </summary>
    private static (DateOnly From, DateOnly To) PreviousQuarter(DateOnly today)
    {
        var thisQuarterStart = new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
        return (thisQuarterStart.AddMonths(-3), thisQuarterStart.AddDays(-1));
    }
}
