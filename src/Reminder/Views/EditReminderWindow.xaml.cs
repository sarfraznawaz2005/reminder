using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ReminderApp.Models;
using ReminderApp.Services;
using ComboBox = System.Windows.Controls.ComboBox;

namespace ReminderApp.Views;

public partial class EditReminderWindow : Window
{
    readonly Reminder? _existing;
    readonly AppSettings _settings;
    readonly List<ToggleButton> _weekdayToggles = new();
    readonly List<ToggleButton> _monthdayToggles = new();
    readonly List<TimeSpan> _times = new();

    public Reminder? Result { get; private set; }

    public EditReminderWindow(Reminder? existing, AppSettings settings)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        _existing = existing;
        _settings = settings;

        Title = existing is null ? "New reminder" : "Edit reminder";

        PopulateHour();
        PopulateMinute(MinuteBox, 1);
        PopulateMinute(HourlyMinuteBox, 1);
        PopulateWeekdayToggles();
        PopulateMonthdayToggles();
        PopulateYearlyMonth();
        PopulateYearlyDay();

        LoadFrom(existing);
    }

    void PopulateHour()
    {
        for (int h = 1; h <= 12; h++) HourBox.Items.Add(new ComboBoxItem { Content = h.ToString() });
    }

    static void PopulateMinute(ComboBox box, int step)
    {
        for (int m = 0; m < 60; m += step) box.Items.Add(new ComboBoxItem { Content = m.ToString("00") });
    }

    void PopulateWeekdayToggles()
    {
        string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        for (int i = 0; i < 7; i++)
        {
            var tb = MakeToggle(names[i], 54);
            _weekdayToggles.Add(tb);
            WeeklyDaysPanel.Children.Add(tb);
        }
    }

    void PopulateMonthdayToggles()
    {
        for (int d = 1; d <= 31; d++)
        {
            var tb = MakeToggle(d.ToString(), 34);
            _monthdayToggles.Add(tb);
            MonthlyDaysPanel.Children.Add(tb);
        }
    }

    static ToggleButton MakeToggle(string text, double size)
    {
        var tb = new ToggleButton
        {
            Content = text,
            Width = size,
            Height = 30,
            Margin = new Thickness(0, 0, 6, 6),
        };
        return tb;
    }

    void PopulateYearlyMonth()
    {
        for (int m = 1; m <= 12; m++)
            YearlyMonthBox.Items.Add(new ComboBoxItem { Content = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m), Tag = m });
    }

    void PopulateYearlyDay()
    {
        for (int d = 1; d <= 31; d++) YearlyDayBox.Items.Add(new ComboBoxItem { Content = d.ToString() });
    }

    void LoadFrom(Reminder? existing)
    {
        var rule = existing?.Rule ?? new RecurrenceRule
        {
            Anchor = DateTime.Today.AddHours(_settings.DefaultHour).AddMinutes(_settings.DefaultMinute),
        };

        TitleBox.Text = existing?.Title ?? "";
        NotesBox.Text = existing?.Notes ?? "";
        RingtoneBox.SelectedIndex = RingtoneIndex(existing?.Ringtone ?? "Default");

        RepeatBox.SelectedIndex = (int)rule.Type;

        int hour12 = rule.Anchor.Hour % 12 == 0 ? 12 : rule.Anchor.Hour % 12;
        HourBox.SelectedIndex = hour12 - 1;
        MinuteBox.SelectedIndex = rule.Anchor.Minute;
        AmPmBox.SelectedIndex = rule.Anchor.Hour >= 12 ? 1 : 0;

        OnceDate.SelectedDate = rule.Type == RepeatType.Once ? rule.Anchor.Date : DateTime.Today;
        HourlyMinuteBox.SelectedIndex = rule.Anchor.Minute;

        var weekdays = rule.DaysOfWeek.Count > 0 ? rule.DaysOfWeek : new List<DayOfWeek> { DateTime.Today.DayOfWeek };
        foreach (var tb in _weekdayToggles) tb.IsChecked = false;
        foreach (var dow in weekdays) _weekdayToggles[(int)dow].IsChecked = true;

        var monthdays = rule.DaysOfMonth.Count > 0 ? rule.DaysOfMonth : new List<int> { DateTime.Today.Day };
        foreach (var tb in _monthdayToggles) tb.IsChecked = false;
        foreach (var day in monthdays) _monthdayToggles[day - 1].IsChecked = true;
        MonthlyClampBox.IsChecked = rule.ClampToMonthEnd;

        YearlyMonthBox.SelectedIndex = (rule.Type == RepeatType.Yearly ? rule.Anchor.Month : DateTime.Today.Month) - 1;
        YearlyDayBox.SelectedIndex = (rule.Type == RepeatType.Yearly ? rule.Anchor.Day : DateTime.Today.Day) - 1;
        YearlyClampBox.IsChecked = rule.ClampToMonthEnd;

        _times.Clear();
        _times.AddRange(rule.TimesOfDay.OrderBy(t => t));
        RenderTimeChips();

        UpdatePanelVisibility();
    }

    static int RingtoneIndex(string ringtone) => ringtone switch
    {
        "Mute" => 0,
        "Default" => 1,
        "Chime" => 2,
        "Alert" => 3,
        _ => 1,
    };

    static string RingtoneName(int index) => index switch
    {
        0 => "Mute",
        2 => "Chime",
        3 => "Alert",
        _ => "Default",
    };

    void RepeatBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePanelVisibility();

    void UpdatePanelVisibility()
    {
        if (RepeatBox.SelectedIndex < 0) return;
        var type = (RepeatType)RepeatBox.SelectedIndex;

        OncePanel.Visibility = type == RepeatType.Once ? Visibility.Visible : Visibility.Collapsed;
        HourlyPanel.Visibility = type == RepeatType.Hourly ? Visibility.Visible : Visibility.Collapsed;
        WeeklyPanel.Visibility = type == RepeatType.Weekly ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPanel.Visibility = type == RepeatType.Monthly ? Visibility.Visible : Visibility.Collapsed;
        YearlyPanel.Visibility = type == RepeatType.Yearly ? Visibility.Visible : Visibility.Collapsed;

        // Hourly only needs minute-past-the-hour, not a full time-of-day picker.
        TimePanel.Visibility = type == RepeatType.Hourly ? Visibility.Collapsed : Visibility.Visible;

        bool supportsMultiTime = SupportsMultiTime(type);
        AddTimeButton.Visibility = supportsMultiTime ? Visibility.Visible : Visibility.Collapsed;
        TimesChipPanel.Visibility = supportsMultiTime ? Visibility.Visible : Visibility.Collapsed;
    }

    static bool SupportsMultiTime(RepeatType type) =>
        type is RepeatType.Daily or RepeatType.Weekly or RepeatType.Monthly or RepeatType.Yearly;

    void AddTime_Click(object sender, RoutedEventArgs e)
    {
        var t = new TimeSpan(SelectedHour24(), MinuteBox.SelectedIndex < 0 ? 0 : MinuteBox.SelectedIndex, 0);
        if (!_times.Contains(t)) _times.Add(t);
        RenderTimeChips();
    }

    void RenderTimeChips()
    {
        TimesChipPanel.Children.Clear();
        foreach (var t in _times.OrderBy(x => x))
        {
            var label = new TextBlock
            {
                Text = DateTime.Today.Add(t).ToString("h:mm tt"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            };
            var remove = new Button
            {
                Content = "✕",
                FontSize = 9,
                Padding = new Thickness(6, 0, 0, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            };
            remove.Click += (_, _) => { _times.Remove(t); RenderTimeChips(); };

            var chip = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("BgBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { label, remove } },
            };
            TimesChipPanel.Children.Add(chip);
        }
    }

    int SelectedHour24()
    {
        int hour12 = HourBox.SelectedIndex + 1;
        bool pm = AmPmBox.SelectedIndex == 1;
        return hour12 % 12 + (pm ? 12 : 0);
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Please enter a title.", "Title required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (RepeatBox.SelectedIndex < 0)
        {
            MessageBox.Show(this, "Please choose a repeat option.", "Repeat required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var type = (RepeatType)RepeatBox.SelectedIndex;
        var rule = new RecurrenceRule { Type = type };

        int minute = MinuteBox.SelectedIndex < 0 ? 0 : MinuteBox.SelectedIndex;
        int hour = SelectedHour24();

        switch (type)
        {
            case RepeatType.Once:
                if (OnceDate.SelectedDate is null)
                {
                    MessageBox.Show(this, "Please choose a date.", "Date required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                rule.Anchor = OnceDate.SelectedDate.Value.Date.AddHours(hour).AddMinutes(minute);
                break;

            case RepeatType.Hourly:
                int hourlyMinute = HourlyMinuteBox.SelectedIndex < 0 ? 0 : HourlyMinuteBox.SelectedIndex;
                rule.Anchor = DateTime.Today.AddMinutes(hourlyMinute);
                break;

            case RepeatType.Daily:
                rule.Anchor = DateTime.Today.AddHours(hour).AddMinutes(minute);
                break;

            case RepeatType.Weekly:
                rule.DaysOfWeek = _weekdayToggles
                    .Select((tb, i) => (tb, i))
                    .Where(x => x.tb.IsChecked == true)
                    .Select(x => (DayOfWeek)x.i)
                    .ToList();
                if (rule.DaysOfWeek.Count == 0)
                {
                    MessageBox.Show(this, "Please choose at least one day.", "Day required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                rule.Anchor = DateTime.Today.AddHours(hour).AddMinutes(minute);
                break;

            case RepeatType.Monthly:
                rule.DaysOfMonth = _monthdayToggles
                    .Select((tb, i) => (tb, i))
                    .Where(x => x.tb.IsChecked == true)
                    .Select(x => x.i + 1)
                    .ToList();
                if (rule.DaysOfMonth.Count == 0)
                {
                    MessageBox.Show(this, "Please choose at least one day.", "Day required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                rule.ClampToMonthEnd = MonthlyClampBox.IsChecked == true;
                rule.Anchor = DateTime.Today.AddHours(hour).AddMinutes(minute);
                break;

            case RepeatType.Yearly:
                int month = YearlyMonthBox.SelectedIndex + 1;
                int day = YearlyDayBox.SelectedIndex + 1;
                rule.ClampToMonthEnd = YearlyClampBox.IsChecked == true;
                rule.Anchor = new DateTime(DateTime.Today.Year, 1, 1).AddHours(hour).AddMinutes(minute);
                rule.Anchor = new DateTime(rule.Anchor.Year, month, Math.Min(day, DateTime.DaysInMonth(rule.Anchor.Year, month)),
                    rule.Anchor.Hour, rule.Anchor.Minute, 0);
                break;
        }

        if (SupportsMultiTime(type) && _times.Count > 0)
        {
            rule.TimesOfDay = _times.OrderBy(t => t).ToList();
            rule.Anchor = rule.Anchor.Date + rule.TimesOfDay[0];
        }

        var reminder = _existing ?? new Reminder();
        reminder.Title = TitleBox.Text.Trim();
        reminder.Notes = NotesBox.Text.Trim();
        reminder.Rule = rule;
        reminder.Ringtone = RingtoneName(RingtoneBox.SelectedIndex);

        Result = reminder;
        DialogResult = true;
    }
}
