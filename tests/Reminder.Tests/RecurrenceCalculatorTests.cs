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
}
