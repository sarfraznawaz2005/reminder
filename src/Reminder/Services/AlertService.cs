using ReminderApp.Models;

namespace ReminderApp.Services;

public sealed class AlertService
{
    readonly ReminderStore _store;
    readonly Scheduler _scheduler;
    readonly AppSettings _settings;

    public event Action<Reminder>? ShowPopupRequested;
    public event Action<Guid>? DismissPopupRequested;

    // 0 means the user turned snooze off in Settings.
    public bool SnoozeEnabled => _settings.DefaultSnoozeMinutes > 0;

    public AlertService(ReminderStore store, Scheduler scheduler, AppSettings settings)
    {
        _store = store;
        _scheduler = scheduler;
        _settings = settings;

        _scheduler.Due += Fire;
    }

    // Scheduler has already advanced/cleared the reminder's cursor before raising Due.
    void Fire(Reminder r)
    {
        r.LastFired = DateTime.Now;
        _store.MarkDirty();

        ShowPopupRequested?.Invoke(r);
        Ringtone.Play(r.Ringtone);
    }

    public void HandleAction(Guid id, string action, int? minutes)
    {
        var r = _store.Reminders.FirstOrDefault(x => x.Id == id);
        if (r is null) return;

        switch (action)
        {
            case "snooze":
                var mins = minutes ?? _settings.DefaultSnoozeMinutes;
                r.SnoozedUntil = DateTime.Now.AddMinutes(mins);
                r.SnoozeInstantUtc = new DateTimeOffset(r.SnoozedUntil.Value, TimeZoneInfo.Local.GetUtcOffset(r.SnoozedUntil.Value));
                _store.MarkDirty();
                break;

            case "done":
                r.SnoozedUntil = null;
                r.SnoozeInstantUtc = null;
                if (r.Rule.Type == RepeatType.Once) r.IsCompleted = true;
                _store.MarkDirty();
                break;
        }

        DismissPopupRequested?.Invoke(id);
    }

    public void MarkComplete(Reminder r)
    {
        r.IsCompleted = true;
        r.IsEnabled = false;
        _store.MarkDirty();
    }
}
