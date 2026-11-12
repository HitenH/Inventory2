namespace Inventory.Service;

/// <summary>
/// The screens keep dates as DateOnly; the MudBlazor date picker works with DateTime? (batch 13).
/// These two helpers convert between them for <c>Shared/DateBox.razor</c>.
/// </summary>
public static class DateConversion
{
    /// <summary>DateOnly or DateOnly? → DateTime? at midnight; empty stays empty.</summary>
    public static DateTime? ToDateTime<TValue>(TValue value) => value switch
    {
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        _ => null
    };

    /// <summary>
    /// DateTime? → DateOnly or DateOnly?. Emptying the box empties a DateOnly? field;
    /// a DateOnly field cannot be empty, so it keeps its current date.
    /// </summary>
    public static TValue FromDateTime<TValue>(DateTime? date, TValue current)
    {
        if (date is null)
        {
            return default(TValue) is null ? default! : current;
        }
        return (TValue)(object)DateOnly.FromDateTime(date.Value);
    }
}
