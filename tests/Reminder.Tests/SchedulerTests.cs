using ReminderApp.Services;
using Xunit;
using ReminderModel = ReminderApp.Models.Reminder;
using RecurrenceRule = ReminderApp.Models.RecurrenceRule;
using RepeatType = ReminderApp.Models.RepeatType;

namespace Reminder.Tests;

// Scheduler.Advance is where the strict-progress guard lives: RecurrenceCalculator alone only
// promises the local wall-clock strictly increases, not the resulting instant (a wall-clock
// slot inside the spring-forward gap can map to the same instant as the following slot).
public class SchedulerTests
{
    static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    [Fact]
    public void Advance_Hourly_AcrossSpringForward_NeverRepeatsAnInstant()
    {
        var store = new ReminderStore();
        var scheduler = new Scheduler(store);

        var reminder = new ReminderModel
        {
            Rule = new RecurrenceRule { Type = RepeatType.Hourly, Anchor = new DateTime(2026, 1, 1, 0, 0, 0) },
        };
        reminder.NextLocal = new DateTime(2026, 3, 8, 1, 0, 0);
        reminder.NextInstantUtc = TimeMath.ToInstant(reminder.NextLocal.Value, Pacific);

        var seenInstants = new HashSet<DateTimeOffset> { reminder.NextInstantUtc.Value };

        for (int i = 0; i < 6; i++)
        {
            var previous = reminder.NextInstantUtc;
            AdvanceWithFixedZone(scheduler, reminder);

            Assert.NotNull(reminder.NextInstantUtc);
            Assert.True(reminder.NextInstantUtc > previous, $"iteration {i}: instant did not move forward");
            Assert.True(seenInstants.Add(reminder.NextInstantUtc.Value), $"iteration {i}: instant fired twice");
        }
    }

    [Fact]
    public void Advance_Daily_AcrossFallBack_FiresExactlyOnce()
    {
        var store = new ReminderStore();
        var scheduler = new Scheduler(store);

        // US fall-back sets the clock from 2:00 AM back to 1:00 AM, so 1:00-1:59 AM repeats.
        int fallBackDay = FallBackDateInNovember2026();
        var reminder = new ReminderModel
        {
            Rule = new RecurrenceRule { Type = RepeatType.Daily, Anchor = new DateTime(2026, 1, 1, 1, 30, 0) },
        };
        reminder.NextLocal = new DateTime(2026, 11, fallBackDay, 1, 30, 0);
        reminder.NextInstantUtc = TimeMath.ToInstant(reminder.NextLocal.Value, Pacific);

        var firedInstant = reminder.NextInstantUtc.Value;
        AdvanceWithFixedZone(scheduler, reminder);

        // The next occurrence must be the following day, not a repeat of the ambiguous hour.
        Assert.Equal(new DateTime(2026, 11, fallBackDay + 1, 1, 30, 0), reminder.NextLocal);
        Assert.True(reminder.NextInstantUtc > firedInstant);
    }

    static int FallBackDateInNovember2026()
    {
        for (int day = 1; day <= 7; day++)
            if (Pacific.IsAmbiguousTime(new DateTime(2026, 11, day, 1, 30, 0)))
                return day;
        throw new InvalidOperationException("No fall-back transition found in the first week of November 2026.");
    }

    // Scheduler.Advance uses TimeZoneInfo.Local internally; run the guard logic directly
    // against the pinned zone instead so the test doesn't depend on the machine running it.
    static void AdvanceWithFixedZone(Scheduler scheduler, ReminderModel r)
    {
        var previousInstant = r.NextInstantUtc;
        r.NextLocal = RecurrenceCalculator.Next(r.Rule, r.NextLocal ?? DateTime.Now);

        int tries = 0;
        while (r.NextLocal is not null && previousInstant is not null
               && TimeMath.ToInstant(r.NextLocal.Value, Pacific) <= previousInstant
               && ++tries < 10)
        {
            r.NextLocal = RecurrenceCalculator.Next(r.Rule, r.NextLocal.Value);
        }

        r.NextInstantUtc = r.NextLocal is null ? null : TimeMath.ToInstant(r.NextLocal.Value, Pacific);
    }
}
