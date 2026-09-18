using ReminderApp.Models;
using ReminderApp.Services;
using Xunit;

namespace Reminder.Tests;

public class RecurrenceCalculatorTests
{
    static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    [Fact]
    public void Daily_SpringForwardGap_FiresAtEndOfGap()
    {
        var rule = new RecurrenceRule { Type = RepeatType.Daily, Anchor = new DateTime(2026, 1, 1, 2, 30, 0) };
        var next = RecurrenceCalculator.Next(rule, new DateTime(2026, 3, 7, 12, 0, 0))!.Value;

        Assert.Equal(new DateTime(2026, 3, 8, 2, 30, 0), next);

        var instant = TimeMath.ToInstant(next, Pacific);
        var expected = TimeMath.ToInstant(new DateTime(2026, 3, 8, 3, 0, 0), Pacific);
        Assert.Equal(expected, instant);
    }

    [Fact]
    public void Daily_FallBackAmbiguous_FiresOnce()
    {
        // US fall-back sets the clock from 2:00 AM back to 1:00 AM, so 1:00-1:59 AM repeats.
        var local = new DateTime(2026, 11, FallBackDateInNovember2026(), 1, 30, 0);
        var instant = TimeMath.ToInstant(local, Pacific);

        // The earlier (DST) pass has the larger UTC offset.
        var offsets = Pacific.GetAmbiguousTimeOffsets(local);
        Assert.Equal(offsets.Max(), instant.Offset);
    }

    // DST transition dates move around (first Sunday of November); find it instead of
    // hardcoding a date that will eventually be wrong.
    static int FallBackDateInNovember2026()
    {
        for (int day = 1; day <= 7; day++)
            if (Pacific.IsAmbiguousTime(new DateTime(2026, 11, day, 1, 30, 0)))
                return day;
        throw new InvalidOperationException("No fall-back transition found in the first week of November 2026.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Monthly_Day31_ClampBehavior(bool clamp)
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Monthly,
            Anchor = new DateTime(2026, 1, 31, 15, 0, 0),
            DaysOfMonth = new List<int> { 31 },
            ClampToMonthEnd = clamp,
        };

        var afterJan = RecurrenceCalculator.Next(rule, new DateTime(2026, 1, 31, 16, 0, 0))!.Value;

        if (clamp)
        {
            // February 2026 has 28 days.
            Assert.Equal(new DateTime(2026, 2, 28, 15, 0, 0), afterJan);
        }
        else
        {
            // February skipped entirely; next hit is March 31.
            Assert.Equal(new DateTime(2026, 3, 31, 15, 0, 0), afterJan);
        }
    }

    [Fact]
    public void Monthly_MultiDay_29_30_31_InFebruary_FiresExactlyOnce()
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Monthly,
            Anchor = new DateTime(2026, 1, 1, 11, 0, 0),
            DaysOfMonth = new List<int> { 29, 30, 31 },
            ClampToMonthEnd = true,
        };

        var first = RecurrenceCalculator.Next(rule, new DateTime(2026, 2, 1, 0, 0, 0))!.Value;
        Assert.Equal(new DateTime(2026, 2, 28, 11, 0, 0), first);

        var second = RecurrenceCalculator.Next(rule, first)!.Value;
        Assert.Equal(new DateTime(2026, 3, 29, 11, 0, 0), second);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Yearly_Feb29_ClampBehavior(bool clamp)
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Yearly,
            Anchor = new DateTime(2024, 2, 29, 9, 0, 0),
            ClampToMonthEnd = clamp,
        };

        var next = RecurrenceCalculator.Next(rule, new DateTime(2027, 1, 1, 0, 0, 0))!.Value;

        if (clamp)
            Assert.Equal(new DateTime(2027, 2, 28, 9, 0, 0), next);
        else
            Assert.Equal(new DateTime(2028, 2, 29, 9, 0, 0), next); // next leap year
    }

    [Fact]
    public void Weekly_MultiDay_WrapsWeekBoundary()
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Weekly,
            Anchor = new DateTime(2026, 1, 5, 9, 0, 0), // a Monday
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday },
        };

        // Starting on Saturday, the next hit should be Monday, not Friday.
        var saturday = new DateTime(2026, 1, 10, 12, 0, 0);
        var next = RecurrenceCalculator.Next(rule, saturday)!.Value;
        Assert.Equal(new DateTime(2026, 1, 12, 9, 0, 0), next);
        Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
    }

    [Fact]
    public void Once_InThePast_ReturnsNull()
    {
        var rule = new RecurrenceRule { Type = RepeatType.Once, Anchor = new DateTime(2020, 1, 1) };
        Assert.Null(RecurrenceCalculator.Next(rule, DateTime.Now));
    }

    [Theory]
    [InlineData(RepeatType.Hourly)]
    [InlineData(RepeatType.Daily)]
    [InlineData(RepeatType.Weekly)]
    [InlineData(RepeatType.Monthly)]
    [InlineData(RepeatType.Yearly)]
    public void StrictProgress_EveryTypeAlwaysMovesForward(RepeatType type)
    {
        var rule = new RecurrenceRule { Type = type, Anchor = new DateTime(2026, 1, 15, 9, 30, 0) };
        var t = new DateTime(2026, 1, 1, 0, 0, 0);

        for (int i = 0; i < 20; i++)
        {
            var next = RecurrenceCalculator.Next(rule, t);
            Assert.NotNull(next);
            Assert.True(next > t, $"type {type}, iteration {i}: {next} should be after {t}");
            t = next.Value;
        }
    }

    [Fact]
    public void Daily_MultipleTimes_FiresEachOneInOrderThenWrapsToTomorrow()
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Daily,
            Anchor = new DateTime(2026, 1, 1, 7, 0, 0),
            TimesOfDay = new List<TimeSpan> { new(13, 0, 0), new(7, 0, 0), new(20, 0, 0) }, // unsorted on purpose
        };

        var t = new DateTime(2026, 1, 5, 6, 0, 0);
        var first = RecurrenceCalculator.Next(rule, t)!.Value;
        Assert.Equal(new DateTime(2026, 1, 5, 7, 0, 0), first);

        var second = RecurrenceCalculator.Next(rule, first)!.Value;
        Assert.Equal(new DateTime(2026, 1, 5, 13, 0, 0), second);

        var third = RecurrenceCalculator.Next(rule, second)!.Value;
        Assert.Equal(new DateTime(2026, 1, 5, 20, 0, 0), third);

        var wrapsToTomorrow = RecurrenceCalculator.Next(rule, third)!.Value;
        Assert.Equal(new DateTime(2026, 1, 6, 7, 0, 0), wrapsToTomorrow);
    }

    [Fact]
    public void Weekly_MultipleTimes_OnSameDayFireInOrder()
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Weekly,
            Anchor = new DateTime(2026, 1, 5, 9, 0, 0), // a Monday
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Monday },
            TimesOfDay = new List<TimeSpan> { new(9, 0, 0), new(18, 0, 0) },
        };

        var afterMorning = RecurrenceCalculator.Next(rule, new DateTime(2026, 1, 5, 10, 0, 0))!.Value;
        Assert.Equal(new DateTime(2026, 1, 5, 18, 0, 0), afterMorning);

        var nextMonday = RecurrenceCalculator.Next(rule, afterMorning)!.Value;
        Assert.Equal(new DateTime(2026, 1, 12, 9, 0, 0), nextMonday);
    }

    [Fact]
    public void Monthly_MultipleTimes_CombinesWithMultipleDays()
    {
        var rule = new RecurrenceRule
        {
            Type = RepeatType.Monthly,
            Anchor = new DateTime(2026, 1, 1, 9, 0, 0),
            DaysOfMonth = new List<int> { 1, 15 },
            TimesOfDay = new List<TimeSpan> { new(9, 0, 0), new(21, 0, 0) },
        };

        // Day 1 9am, day 1 9pm, day 15 9am, day 15 9pm, in that order.
        var t = new DateTime(2026, 3, 1, 8, 0, 0);
        var expected = new[]
        {
            new DateTime(2026, 3, 1, 9, 0, 0),
            new DateTime(2026, 3, 1, 21, 0, 0),
            new DateTime(2026, 3, 15, 9, 0, 0),
            new DateTime(2026, 3, 15, 21, 0, 0),
            new DateTime(2026, 4, 1, 9, 0, 0),
        };
        foreach (var e in expected)
        {
            t = RecurrenceCalculator.Next(rule, t)!.Value;
            Assert.Equal(e, t);
        }
    }

    [Fact]
    public void EffectiveTimes_EmptyList_FallsBackToAnchorTimeOfDay()
    {
        var rule = new RecurrenceRule { Type = RepeatType.Daily, Anchor = new DateTime(2026, 1, 1, 14, 30, 0) };
        var times = RecurrenceCalculator.EffectiveTimes(rule);
        Assert.Single(times);
        Assert.Equal(new TimeSpan(14, 30, 0), times[0]);
    }
}

public class BackwardCompatibilityTests
{
    // A reminders export captured before TimesOfDay existed - proves files people already
    // exported still import cleanly after this feature landed.
    const string OldExportJson = """
        {
          "version": 1,
          "reminders": [
            {
              "id": "8f1d5c3a-0c2e-4a1b-9f77-2b1c6d5e4a01",
              "title": "Old export, no timesOfDay field",
              "notes": "",
              "rule": {
                "type": "Daily",
                "anchor": "2026-01-17T09:00:00",
                "daysOfWeek": [],
                "daysOfMonth": [],
                "clampToMonthEnd": true
              },
              "ringtone": "Default",
              "isEnabled": true,
              "isCompleted": false,
              "snoozedUntil": null,
              "lastFired": null,
              "createdAt": "2026-01-01T00:00:00"
            }
          ]
        }
        """;

    [Fact]
    public void OldExportFile_WithoutTimesOfDayField_DeserializesWithEmptyList()
    {
        var file = System.Text.Json.JsonSerializer.Deserialize<ReminderFile>(OldExportJson, ReminderApp.Services.Storage.Json);

        Assert.NotNull(file);
        Assert.Single(file!.Reminders);
        var r = file.Reminders[0];
        Assert.Equal("Old export, no timesOfDay field", r.Title);
        Assert.Empty(r.Rule.TimesOfDay);
        Assert.Equal(new DateTime(2026, 1, 17, 9, 0, 0), r.Rule.Anchor);

        // Confirm it still computes a valid next occurrence the same as before this feature existed.
        var next = RecurrenceCalculator.Next(r.Rule, new DateTime(2026, 1, 17, 10, 0, 0));
        Assert.Equal(new DateTime(2026, 1, 18, 9, 0, 0), next);
    }
}
