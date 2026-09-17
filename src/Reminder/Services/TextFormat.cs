using System.Globalization;
using ReminderApp.Models;

namespace ReminderApp.Services;

public static class TextFormat
{
    public static string Countdown(Reminder r)
    {
        if (r.SnoozedUntil is not null) return Countdown(r.SnoozedUntil.Value, "Snoozed");
        if (r.NextLocal is null) return r.Rule.Type == RepeatType.Once && r.LastFired is not null ? "Missed" : "";
        return Countdown(r.NextLocal.Value, null);
    }

    static string Countdown(DateTime target, string? prefix)
    {
        var span = target - DateTime.Now;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;

        var parts = new List<string>();
        if (span.Days > 0) parts.Add($"{span.Days} Day{(span.Days == 1 ? "" : "s")}");
        if (span.Hours > 0 || parts.Count > 0) parts.Add($"{span.Hours} Hour{(span.Hours == 1 ? "" : "s")}");
        parts.Add($"{span.Minutes} Minute{(span.Minutes == 1 ? "" : "s")}");

        var text = string.Join(" ", parts);
        return prefix is null ? text : $"{prefix}: {text}";
    }

    public static string ScheduleSummary(RecurrenceRule rule)
    {
        var time = rule.Anchor.ToString("h:mm:ss tt", CultureInfo.InvariantCulture);

        return rule.Type switch
        {
            RepeatType.Once => $"Once: {rule.Anchor:ddd d MMM yyyy} ({time})",
            RepeatType.Hourly => $"Hourly: (:{rule.Anchor:mm:ss})",
            RepeatType.Daily => $"Daily: ({time})",
            RepeatType.Weekly => $"Weekly: {WeeklyDays(rule)} ({time})",
            RepeatType.Monthly => $"Monthly: {MonthlyDays(rule)} ({time})",
            RepeatType.Yearly => $"Yearly: ({rule.Anchor:MM dd} {time})",
            _ => "",
        };
    }

    static string WeeklyDays(RecurrenceRule rule)
    {
        var days = rule.DaysOfWeek.Count > 0 ? rule.DaysOfWeek : new List<DayOfWeek> { rule.Anchor.DayOfWeek };
        return string.Join(" ;", days.OrderBy(d => (int)d).Select(d => d.ToString()[..3]));
    }

    static string MonthlyDays(RecurrenceRule rule)
    {
        var days = rule.DaysOfMonth.Count > 0 ? rule.DaysOfMonth : new List<int> { rule.Anchor.Day };
        return string.Join(" ;", days.OrderBy(d => d));
    }
}
