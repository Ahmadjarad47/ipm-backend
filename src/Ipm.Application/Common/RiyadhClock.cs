namespace Ipm.Application.Common;

/// <summary>
/// "Today" for overdue work and every date filter (R13): the calendar date in the organization time zone (spec 09 R5-f). It starts as Riyadh
/// (UTC+3, no daylight saving, so it works even where tzdata is not installed) and follows the organization setting once that is read.
/// </summary>
public static class RiyadhClock
{
    private static readonly TimeZoneInfo Riyadh = TimeZoneInfo.CreateCustomTimeZone("Asia/Riyadh", TimeSpan.FromHours(3), "Asia/Riyadh", "Asia/Riyadh");

    private static volatile TimeZoneInfo zone = Riyadh;

    public static string ZoneId => zone.Id;

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);

    /// <summary>The calendar day of an instant in the organization time zone.</summary>
    public static DateOnly DayOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>True when the id is an installed (IANA) time zone; Asia/Riyadh is always accepted.</summary>
    public static bool IsValid(string? id) => Find(id) is not null;

    /// <summary>Switches the organization zone; an unknown id leaves it as it was.</summary>
    public static void Use(string id) => zone = Find(id) ?? zone;

    private static TimeZoneInfo? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (id == "Asia/Riyadh")
        {
            return Riyadh;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
