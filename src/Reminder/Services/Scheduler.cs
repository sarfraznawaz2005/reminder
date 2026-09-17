using System.Windows.Threading;
using Microsoft.Win32;
using ReminderApp.Models;

namespace ReminderApp.Services;

// One DispatcherTimer for the whole app. Compares absolute instants (never wall-clock),
// which is what makes "no double-fire during the repeated fall-back hour" structural.
public sealed class Scheduler
{
    static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(5);
    const int MaxAdvanceGuardTries = 10;

    readonly ReminderStore _store;
    readonly DispatcherTimer _timer;
    long _lastTickCount;
    DateTimeOffset _lastTickUtc;

    public event Action<Reminder>? Due;
    public event Action? Tick;

    public Scheduler(ReminderStore store)
    {
        _store = store;
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTick();
    }

    public void Start()
    {
        RecomputeAll();
        _lastTickCount = Environment.TickCount64;
        _lastTickUtc = DateTimeOffset.UtcNow;
        _timer.Start();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
    }

    public void Stop()
    {
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
    }

    void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) RecomputeAll();
    }

    void OnTimeChanged(object? sender, EventArgs e)
    {
        TimeZoneInfo.ClearCachedData();
        RecomputeAll();
    }

    void OnTick()
    {
        var now = DateTimeOffset.UtcNow;

        // Belt-and-braces clock-jump detection: TickCount64 is monotonic and immune to
        // manual clock changes, so a divergence from UtcNow means the wall clock moved.
        var expectedElapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastTickCount);
        var actualElapsed = now - _lastTickUtc;
        if ((actualElapsed - expectedElapsed).Duration() > TimeSpan.FromSeconds(60))
            RecomputeAll();

        _lastTickCount = Environment.TickCount64;
        _lastTickUtc = now;

        foreach (var r in _store.Reminders)
        {
            if (!r.IsEnabled || r.IsCompleted) continue;

            bool fromSnooze = r.SnoozeInstantUtc is not null
                && (r.NextInstantUtc is null || r.SnoozeInstantUtc <= r.NextInstantUtc);
            var target = fromSnooze ? r.SnoozeInstantUtc : r.NextInstantUtc;
            if (target is null || now < target) continue;

            if (now - target > GraceWindow)
            {
                // Missed while asleep/closed: roll forward quietly, do not alert.
                if (fromSnooze) r.SnoozeInstantUtc = r.SnoozedUntil = null;
                else Advance(r);
                continue;
            }

            // Advance/clear before raising Due so a slow UI handler can't cause a re-fire
            // on the next tick.
            if (fromSnooze) r.SnoozeInstantUtc = r.SnoozedUntil = null;
            else Advance(r);

            Due?.Invoke(r);
        }

        _store.FlushIfDirty();
        Tick?.Invoke();
    }

    public void Advance(Reminder r)
    {
        var tz = TimeZoneInfo.Local;
        var previousInstant = r.NextInstantUtc;

        r.NextLocal = RecurrenceCalculator.Next(r.Rule, r.NextLocal ?? DateTime.Now);

        int tries = 0;
        while (r.NextLocal is not null && previousInstant is not null
               && TimeMath.ToInstant(r.NextLocal.Value, tz) <= previousInstant
               && ++tries < MaxAdvanceGuardTries)
        {
            r.NextLocal = RecurrenceCalculator.Next(r.Rule, r.NextLocal.Value);
        }

        r.NextInstantUtc = r.NextLocal is null ? null : TimeMath.ToInstant(r.NextLocal.Value, tz);
    }

    public void RecomputeAll()
    {
        var tz = TimeZoneInfo.Local;
        var now = DateTime.Now;

        foreach (var r in _store.Reminders)
        {
            r.NextLocal = RecurrenceCalculator.Next(r.Rule, now);
            r.NextInstantUtc = r.NextLocal is null ? null : TimeMath.ToInstant(r.NextLocal.Value, tz);
            r.SnoozeInstantUtc = r.SnoozedUntil is null ? null : new DateTimeOffset(r.SnoozedUntil.Value, tz.GetUtcOffset(r.SnoozedUntil.Value));
        }
    }
}
