using System.Windows;
using System.Windows.Controls;
using ReminderApp.Models;
using ReminderApp.Services;

namespace ReminderApp.Views;

public partial class SettingsWindow : Window
{
    readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        _settings = settings;

        for (int h = 1; h <= 12; h++) DefaultHourBox.Items.Add(new ComboBoxItem { Content = h.ToString() });
        for (int m = 0; m < 60; m++) DefaultMinuteBox.Items.Add(new ComboBoxItem { Content = m.ToString("00") });

        LoadFrom(settings);
    }

    void LoadFrom(AppSettings s)
    {
        SnoozeEnabledBox.IsChecked = s.DefaultSnoozeMinutes > 0;

        int hour12 = s.DefaultHour % 12 == 0 ? 12 : s.DefaultHour % 12;
        DefaultHourBox.SelectedIndex = hour12 - 1;
        DefaultMinuteBox.SelectedIndex = s.DefaultMinute;
        DefaultAmPmBox.SelectedIndex = s.DefaultHour >= 12 ? 1 : 0;

        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        CloseToTrayBox.IsChecked = s.CloseToTray;
        MinimizeToTrayBox.IsChecked = s.MinimizeToTray;
        StartMinimizedBox.IsChecked = s.StartMinimized;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.DefaultSnoozeMinutes = SnoozeEnabledBox.IsChecked == true ? 10 : 0;

        int hour12 = DefaultHourBox.SelectedIndex + 1;
        bool pm = DefaultAmPmBox.SelectedIndex == 1;
        _settings.DefaultHour = hour12 % 12 + (pm ? 12 : 0);
        _settings.DefaultMinute = DefaultMinuteBox.SelectedIndex < 0 ? 0 : DefaultMinuteBox.SelectedIndex;

        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.CloseToTray = CloseToTrayBox.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true;
        _settings.StartMinimized = StartMinimizedBox.IsChecked == true;

        StartupRegistration.Apply(_settings.StartWithWindows);
        Storage.SaveSettings(_settings);

        DialogResult = true;
    }
}
