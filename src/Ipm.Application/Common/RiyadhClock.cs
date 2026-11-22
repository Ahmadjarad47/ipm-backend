namespace Ipm.Application.Common;

/// <summary>
/// "Today" for overdue work (R13): the calendar date in Riyadh (UTC+3, no daylight saving). A constant until the organization
/// settings exist (part 09), so it does not depend on tzdata being installed in the container.
/// </summary>
public static class RiyadhClock
{
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
}
