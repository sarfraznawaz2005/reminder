using ReminderApp.Models;

namespace ReminderApp.Services;

// Pure calendar math. No DateTime.Now, no timezone knowledge, fully unit-testable.
// Next() returns the first local wall-clock occurrence strictly after afterLocal, or null.
public static class RecurrenceCalculator
{
    const int MaxMonthlyIterations = 48;
    const int MaxYearlyIterations = 8;

    public static DateTime? Next(RecurrenceRule rule, DateTime afterLocal) => rule.Type switch
    {
        RepeatType.Once => NextOnce(rule, afterLocal),
        RepeatType.Hourly => NextHourly(rule, afterLocal),
        RepeatType.Daily => NextDaily(rule, afterLocal),
        RepeatType.Weekly => NextWeekly(rule, afterLocal),
        RepeatType.Monthly => NextMonthly(rule, afterLocal),
        RepeatType.Yearly => NextYearly(rule, afterLocal),
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    static DateTime? NextOnce(RecurrenceRule rule, DateTime afterLocal) =>
        rule.Anchor > afterLocal ? rule.Anchor : null;

    static DateTime? NextHourly(RecurrenceRule rule, DateTime afterLocal)
    {
        var slot = new DateTime(afterLocal.Year, afterLocal.Month, afterLocal.Day, afterLocal.Hour, 0, 0)
            .AddMinutes(rule.Anchor.Minute).AddSeconds(rule.Anchor.Second);

        if (slot <= afterLocal) slot = slot.AddHours(1);
        if (slot < rule.Anchor) slot = rule.Anchor;
        return slot;
    }

    static DateTime? NextDaily(RecurrenceRule rule, DateTime afterLocal)
    {
        var candidate = afterLocal.Date + rule.Anchor.TimeOfDay;
        if (candidate <= afterLocal) candidate = afterLocal.Date.AddDays(1) + rule.Anchor.TimeOfDay;
        if (candidate < rule.Anchor) candidate = rule.Anchor;
        return candidate;
    }

    static DateTime? NextWeekly(RecurrenceRule rule, DateTime afterLocal)
    {
        var days = rule.DaysOfWeek.Count > 0 ? rule.DaysOfWeek : new List<DayOfWeek> { rule.Anchor.DayOfWeek };

        // The candidate must be both > afterLocal and >= Anchor. Using Anchor - 1 tick as the
        // floor when the rule hasn't started yet folds both constraints into one comparison,
        // so the fixed 8-day scan window still starts from the correct date - otherwise a rule
        // whose Anchor is more than a week ahead of afterLocal could scan right past it and
        // wrongly return null.
        var floor = afterLocal >= rule.Anchor ? afterLocal : rule.Anchor.AddTicks(-1);

        for (var d = floor.Date; d < floor.Date.AddDays(8); d = d.AddDays(1))
        {
            if (!days.Contains(d.DayOfWeek)) continue;
            var candidate = d + rule.Anchor.TimeOfDay;
            if (candidate > floor) return candidate;
        }
        return null;
    }

    static DateTime? NextMonthly(RecurrenceRule rule, DateTime afterLocal)
    {
        var days = rule.DaysOfMonth.Count > 0 ? rule.DaysOfMonth : new List<int> { rule.Anchor.Day };
        var month = new DateTime(afterLocal.Year, afterLocal.Month, 1);

        for (int i = 0; i < MaxMonthlyIterations; i++, month = month.AddMonths(1))
        {
            int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var candidates = new SortedSet<int>();

            foreach (var d in days)
            {
                if (d <= daysInMonth) candidates.Add(d);
                else if (rule.ClampToMonthEnd) candidates.Add(daysInMonth);
            }

            foreach (var day in candidates)
            {
                var candidate = new DateTime(month.Year, month.Month, day) + rule.Anchor.TimeOfDay;
                if (candidate > afterLocal && candidate >= rule.Anchor) return candidate;
            }
        }
        return null;
    }

    static DateTime? NextYearly(RecurrenceRule rule, DateTime afterLocal)
    {
        for (int y = afterLocal.Year; y <= afterLocal.Year + MaxYearlyIterations; y++)
        {
            int month = rule.Anchor.Month;
            int day = rule.Anchor.Day;
            int daysInMonth = DateTime.DaysInMonth(y, month);

            if (day > daysInMonth)
            {
                if (!rule.ClampToMonthEnd) continue;
                day = daysInMonth;
            }

            var candidate = new DateTime(y, month, day) + rule.Anchor.TimeOfDay;
            if (candidate > afterLocal && candidate >= rule.Anchor) return candidate;
        }
        return null;
    }
}
