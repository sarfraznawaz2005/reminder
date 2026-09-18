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

    // Sorted so every caller that walks day-by-day naturally visits times in chronological
    // order within each day.
    public static List<TimeSpan> EffectiveTimes(RecurrenceRule rule) =>
        rule.TimesOfDay.Count > 0
            ? rule.TimesOfDay.OrderBy(t => t).ToList()
            : new List<TimeSpan> { rule.Anchor.TimeOfDay };

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
        var times = EffectiveTimes(rule);

        // The candidate must be both > afterLocal and >= Anchor; folding both into one floor
        // (Anchor - 1 tick when the rule hasn't started yet) keeps the fixed scan window
        // correct even when Anchor is further ahead than the window would otherwise cover.
        var floor = afterLocal >= rule.Anchor ? afterLocal : rule.Anchor.AddTicks(-1);

        for (var d = floor.Date; d <= floor.Date.AddDays(1); d = d.AddDays(1))
        {
            foreach (var t in times)
            {
                var candidate = d + t;
                if (candidate > floor) return candidate;
            }
        }
        return null;
    }

    static DateTime? NextWeekly(RecurrenceRule rule, DateTime afterLocal)
    {
        var days = rule.DaysOfWeek.Count > 0 ? rule.DaysOfWeek : new List<DayOfWeek> { rule.Anchor.DayOfWeek };
        var times = EffectiveTimes(rule);
        var floor = afterLocal >= rule.Anchor ? afterLocal : rule.Anchor.AddTicks(-1);

        for (var d = floor.Date; d < floor.Date.AddDays(8); d = d.AddDays(1))
        {
            if (!days.Contains(d.DayOfWeek)) continue;
            foreach (var t in times)
            {
                var candidate = d + t;
                if (candidate > floor) return candidate;
            }
        }
        return null;
    }

    static DateTime? NextMonthly(RecurrenceRule rule, DateTime afterLocal)
    {
        var days = rule.DaysOfMonth.Count > 0 ? rule.DaysOfMonth : new List<int> { rule.Anchor.Day };
        var times = EffectiveTimes(rule);
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
                foreach (var t in times)
                {
                    var candidate = new DateTime(month.Year, month.Month, day) + t;
                    if (candidate > afterLocal && candidate >= rule.Anchor) return candidate;
                }
            }
        }
        return null;
    }

    static DateTime? NextYearly(RecurrenceRule rule, DateTime afterLocal)
    {
        var times = EffectiveTimes(rule);

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

            foreach (var t in times)
            {
                var candidate = new DateTime(y, month, day) + t;
                if (candidate > afterLocal && candidate >= rule.Anchor) return candidate;
            }
        }
        return null;
    }
}
