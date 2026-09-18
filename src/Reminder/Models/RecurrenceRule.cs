namespace ReminderApp.Models;

public enum RepeatType { Once, Hourly, Daily, Weekly, Monthly, Yearly }

public sealed class RecurrenceRule
{
    public RepeatType Type { get; set; } = RepeatType.Once;

    // Local wall-clock time, DateTimeKind.Unspecified. Meaning depends on Type:
    //   Once    -> the exact date+time to fire
    //   Hourly  -> minute/second within each hour; date+time is the "not before" floor
    //   Daily   -> time-of-day; date is the floor
    //   Weekly  -> time-of-day; weekday falls back to Anchor.DayOfWeek if DaysOfWeek is empty
    //   Monthly -> time-of-day; day falls back to Anchor.Day if DaysOfMonth is empty
    //   Yearly  -> month + day + time-of-day
    public DateTime Anchor { get; set; } = RoundToNextClean(DateTime.Now);

    public List<DayOfWeek> DaysOfWeek { get; set; } = new();
    public List<int> DaysOfMonth { get; set; } = new();

    // Fire times for Daily/Weekly/Monthly/Yearly. Same empty-means-derive-from-Anchor pattern
    // as DaysOfWeek/DaysOfMonth: empty means "just Anchor.TimeOfDay" (today's single-time
    // behavior); non-empty replaces it so the reminder fires once per listed time.
    public List<TimeSpan> TimesOfDay { get; set; } = new();

    // Monthly: day 31 in a 30-day month fires on the last day instead of being skipped.
    // Yearly: Feb 29 in a non-leap year fires Feb 28 instead of being skipped.
    public bool ClampToMonthEnd { get; set; } = true;

    static DateTime RoundToNextClean(DateTime now) =>
        new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
}
