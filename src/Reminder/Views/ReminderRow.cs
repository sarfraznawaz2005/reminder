using ReminderApp.Models;
using ReminderApp.Services;

namespace ReminderApp.Views;

// Per-row view wrapper: exposes the live countdown/schedule text a ListBox can bind to,
// and Refresh() is called once a second by MainWindow off the scheduler's Tick event.
public sealed class ReminderRow : ObservableObject
{
    public Reminder Model { get; }

    public ReminderRow(Reminder model) => Model = model;

    public string Title => Model.Title;
    public string Notes => Model.Notes;

    string _countdown = "";
    public string Countdown { get => _countdown; private set => Set(ref _countdown, value); }

    bool _hasCountdown;
    public bool HasCountdown { get => _hasCountdown; private set => Set(ref _hasCountdown, value); }

    string _schedule = "";
    public string Schedule { get => _schedule; private set => Set(ref _schedule, value); }

    // Pure UI state for bulk actions - never persisted.
    bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    public void Refresh()
    {
        Countdown = TextFormat.Countdown(Model);
        HasCountdown = !string.IsNullOrEmpty(Countdown);
        Schedule = TextFormat.ScheduleSummary(Model.Rule);
    }

    public void RaiseAll()
    {
        Raise(nameof(Title));
        Raise(nameof(Notes));
        Refresh();
    }
}
