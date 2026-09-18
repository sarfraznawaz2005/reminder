using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ReminderApp.Models;
using ReminderApp.Services;

namespace ReminderApp.Views;

public partial class MainWindow : Window
{
    readonly ReminderStore _store;
    readonly Scheduler _scheduler;
    readonly AlertService _alerts;
    readonly AppSettings _settings;

    readonly ObservableCollection<ReminderRow> _rows = new();
    readonly Dictionary<Guid, ReminderRow> _rowsById = new();
    string _activeTab = "Active";

    public event Action? ExitRequested;

    public MainWindow(ReminderStore store, Scheduler scheduler, AlertService alerts, AppSettings settings)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _store = store;
        _scheduler = scheduler;
        _alerts = alerts;
        _settings = settings;

        List.ItemsSource = _rows;

        _store.Reminders.CollectionChanged += OnStoreChanged;
        // MarkDirty (fired on complete/snooze/edit) doesn't touch the collection itself -
        // it only changes a property on an existing item - so CollectionChanged never fires
        // for it. Without this, completing a reminder wouldn't move it out of Active
        // until something else happened to force a rebuild, like switching tabs.
        _store.Changed += RebuildVisibleRows;
        foreach (var r in _store.Reminders) TrackRow(r);
        RebuildVisibleRows();

        _scheduler.Tick += OnSchedulerTick;

        RestorePlacement();
        StateChanged += (_, _) => OnStateChangedHandler();
        Closing += OnClosingHandler;
    }

    void OnStoreChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _rowsById.Clear();
            foreach (var r in _store.Reminders) TrackRow(r);
        }
        else
        {
            if (e.OldItems is not null)
                foreach (Reminder r in e.OldItems) _rowsById.Remove(r.Id);
            if (e.NewItems is not null)
                foreach (Reminder r in e.NewItems) TrackRow(r);
        }
        RebuildVisibleRows();
    }

    void TrackRow(Reminder r)
    {
        var row = new ReminderRow(r);
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReminderRow.IsSelected)) UpdateSelectionBar();
        };
        _rowsById[r.Id] = row;
    }

    static DateTime? EffectiveNext(Reminder r) =>
        (r.SnoozedUntil, r.NextLocal) switch
        {
            (null, null) => null,
            (null, var next) => next,
            (var snooze, null) => snooze,
            (var snooze, var next) => snooze < next ? snooze : next,
        };

    void RebuildVisibleRows()
    {
        _rows.Clear();
        bool wantCompleted = _activeTab == "Completed";

        var visible = _store.Reminders.Where(r => r.IsCompleted == wantCompleted);
        visible = wantCompleted
            // Completed: most recently run first.
            ? visible.OrderByDescending(r => r.LastFired ?? DateTime.MinValue)
            // Active: soonest due first - snooze counts too, since that's what will
            // actually alert next.
            : visible.OrderBy(r => EffectiveNext(r) ?? DateTime.MaxValue);

        foreach (var r in visible)
        {
            var row = _rowsById[r.Id];
            row.RaiseAll();
            _rows.Add(row);
        }
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ActiveTab.Content = $"Active ({_store.Reminders.Count(r => !r.IsCompleted)})";
        CompletedTab.Content = $"Completed ({_store.Reminders.Count(r => r.IsCompleted)})";

        UpdateSelectionBar();
    }

    void OnSchedulerTick()
    {
        foreach (var row in _rows) row.Refresh();
    }

    void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        _activeTab = tag;
        // Selection is per-tab in spirit - carrying it across tabs risks bulk-completing
        // items the user never looked at while on this tab.
        foreach (var row in _rowsById.Values) row.IsSelected = false;
        RebuildVisibleRows();
    }

    void UpdateSelectionBar()
    {
        var selectedCount = _rows.Count(r => r.IsSelected);

        var allSelected = _rows.Count > 0 && selectedCount == _rows.Count;
        SelectAllButtonSelection.Content = allSelected ? "Deselect all" : "Select all";

        if (selectedCount == 0)
        {
            SelectionActions.Visibility = Visibility.Collapsed;
            NormalActions.Visibility = Visibility.Visible;
            return;
        }

        NormalActions.Visibility = Visibility.Collapsed;
        SelectionActions.Visibility = Visibility.Visible;
        SelectionCountText.Text = selectedCount == 1 ? "1 selected" : $"{selectedCount} selected";
        // Completed items are already complete - only offer this on the Active tab.
        BulkCompleteButton.Visibility = _activeTab == "Active" ? Visibility.Visible : Visibility.Collapsed;
    }

    void SelectAllToggle_Click(object sender, RoutedEventArgs e)
    {
        var allSelected = _rows.Count > 0 && _rows.All(r => r.IsSelected);
        foreach (var row in _rows) row.IsSelected = !allSelected;
        UpdateSelectionBar();
    }

    void BulkDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = _rows.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0) return;

        var result = MessageBox.Show(this, $"Delete {selected.Count} reminder(s)?", "Delete reminders",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        foreach (var row in selected) _store.Remove(row.Model);
    }

    void BulkComplete_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows.Where(r => r.IsSelected).ToList())
        {
            row.IsSelected = false;
            _alerts.MarkComplete(row.Model);
        }
    }

    void BulkCancel_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rowsById.Values) row.IsSelected = false;
    }

    void AddNew_Click(object sender, RoutedEventArgs e) => OpenAddNew();

    public void OpenAddNew()
    {
        var editor = new EditReminderWindow(null, _settings) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } reminder)
        {
            _store.Add(reminder);
            _scheduler.RecomputeAll();
        }
    }

    void List_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var current = source;
        while (current is not null)
        {
            // Let interactive controls (the row checkbox, edit/complete/delete buttons) handle their own click.
            if (current is CheckBox or System.Windows.Controls.Primitives.ButtonBase) return;
            if (current is ListBoxItem { DataContext: ReminderRow row })
            {
                row.IsSelected = !row.IsSelected;
                return;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
    }

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ReminderRow row }) return;
        EditSelected(row.Model);
    }

    void EditSelected(Reminder model)
    {
        var editor = new EditReminderWindow(model, _settings) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            _store.MarkDirty();
            _scheduler.RecomputeAll();
            RebuildVisibleRows();
        }
    }

    void MarkComplete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ReminderRow row }) return;
        _alerts.MarkComplete(row.Model);
        RebuildVisibleRows();
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ReminderRow row }) return;
        var result = MessageBox.Show(this, $"Delete \"{row.Model.Title}\"?", "Delete reminder",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes) _store.Remove(row.Model);
    }

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings) { Owner = this };
        settingsWindow.ShowDialog();
    }

    void ImportExport_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var export = new MenuItem { Header = "Export reminders..." };
        export.Click += (_, _) => ExportReminders();
        menu.Items.Add(export);

        var import = new MenuItem { Header = "Import reminders..." };
        import.Click += (_, _) => ImportReminders();
        menu.Items.Add(import);

        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    void ExportReminders()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export reminders",
            Filter = "JSON file (*.json)|*.json",
            FileName = "reminders.json",
        };
        if (dialog.ShowDialog(this) != true) return;

        var file = new ReminderFile { Reminders = _store.Reminders.ToList() };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(file, Storage.Json));
        MessageBox.Show(this, $"Exported {file.Reminders.Count} reminder(s).", "Export complete",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void ImportReminders()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import reminders",
            Filter = "JSON file (*.json)|*.json",
        };
        if (dialog.ShowDialog(this) != true) return;

        ReminderFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ReminderFile>(File.ReadAllText(dialog.FileName), Storage.Json);
        }
        catch
        {
            MessageBox.Show(this, "That file isn't a valid reminders export.", "Import failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (file is null || file.Reminders.Count == 0)
        {
            MessageBox.Show(this, "No reminders found in that file.", "Import",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Imported reminders are added as new entries, never overwriting what's already here -
        // fresh IDs so they can't collide with anything currently in the list.
        foreach (var r in file.Reminders)
        {
            r.Id = Guid.NewGuid();
            _store.Add(r);
        }
        _scheduler.RecomputeAll();

        MessageBox.Show(this, $"Imported {file.Reminders.Count} reminder(s).", "Import complete",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    void OnStateChangedHandler()
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
            Hide();
    }

    void OnClosingHandler(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.IsExiting) { SavePlacement(); return; }

        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            SavePlacement();
            Hide();
        }
        else
        {
            // With "close to tray" off, the X should fully quit - not just close this
            // window while ShutdownMode=OnExplicitShutdown leaves the app (and tray icon)
            // running invisibly in the background. Route through the real exit path instead.
            e.Cancel = true;
            SavePlacement();
            ExitRequested?.Invoke();
        }
    }

    void RestorePlacement()
    {
        var w = _settings.Window;
        Width = w.Width;
        Height = w.Height;

        if (double.IsNaN(w.Left) || double.IsNaN(w.Top))
        {
            // First run, or placement was never saved: center on screen instead of
            // whatever default position Windows would otherwise pick.
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            Left = w.Left;
            Top = w.Top;
        }

        if (w.Maximized) WindowState = WindowState.Maximized;
    }

    public void SavePlacement()
    {
        var w = _settings.Window;
        w.Maximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            w.Left = Left;
            w.Top = Top;
            w.Width = Width;
            w.Height = Height;
        }
        Storage.SaveSettings(_settings);
    }
}
