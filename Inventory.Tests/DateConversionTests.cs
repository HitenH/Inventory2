using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Batch 13: the MudBlazor date box converts between the screens' DateOnly and the picker's DateTime?.</summary>
public class DateConversionTests
{
    private static readonly DateOnly Day = new(2026, 7, 1);

    [Fact]
    public void Dates_go_to_the_picker_at_midnight_and_come_back_unchanged()
    {
        Assert.Equal(new DateTime(2026, 7, 1), DateConversion.ToDateTime(Day));
        Assert.Equal(new DateTime(2026, 7, 1), DateConversion.ToDateTime<DateOnly?>(Day));
        Assert.Equal(Day, DateConversion.FromDateTime<DateOnly>(new DateTime(2026, 7, 1, 15, 30, 0), default));
        Assert.Equal(Day, DateConversion.FromDateTime<DateOnly?>(new DateTime(2026, 7, 1), null));
    }

    [Fact]
    public void An_empty_box_empties_an_optional_date()
    {
        Assert.Null(DateConversion.ToDateTime<DateOnly?>(null));
        Assert.Null(DateConversion.FromDateTime<DateOnly?>(null, Day));
    }

    [Fact]
    public void A_required_date_keeps_its_value_when_the_box_is_emptied()
        => Assert.Equal(Day, DateConversion.FromDateTime<DateOnly>(null, Day));
}
