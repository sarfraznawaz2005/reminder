using System.Collections.ObjectModel;
using ReminderApp.Models;

namespace ReminderApp.Services;

public sealed class ReminderStore
{
    public ObservableCollection<Reminder> Reminders { get; } = new();

    bool _dirty;

    public event Action? Changed;

    public void Load()
    {
        var file = Storage.LoadReminders();
        Reminders.Clear();
        foreach (var r in file.Reminders) Reminders.Add(r);
    }

    public void Add(Reminder reminder)
    {
        Reminders.Add(reminder);
        MarkDirty();
    }

    public void Remove(Reminder reminder)
    {
        Reminders.Remove(reminder);
        MarkDirty();
    }

    public void MarkDirty()
    {
        _dirty = true;
        Changed?.Invoke();
    }

    // Called by the scheduler tick at most once per second; a no-op when nothing changed.
    public void FlushIfDirty()
    {
        if (!_dirty) return;
        SaveNow();
    }

    public void SaveNow()
    {
        Storage.SaveReminders(new ReminderFile { Reminders = Reminders.ToList() });
        _dirty = false;
    }
}
