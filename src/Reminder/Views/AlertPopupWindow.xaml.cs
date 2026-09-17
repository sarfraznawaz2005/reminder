using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using ReminderApp.Models;
using ReminderApp.Services;

namespace ReminderApp.Views;

public partial class AlertPopupWindow : Window
{
    static readonly List<AlertPopupWindow> Open = new();
    const double Gap = 10;

    readonly AlertService _alerts;
    readonly Guid _reminderId;

    public AlertPopupWindow(Reminder reminder, AlertService alerts)
    {
        InitializeComponent();
        _alerts = alerts;
        _reminderId = reminder.Id;

        TitleText.Text = reminder.Title;
        NotesText.Text = reminder.Notes;
        NotesText.Visibility = string.IsNullOrWhiteSpace(reminder.Notes) ? Visibility.Collapsed : Visibility.Visible;

        if (!_alerts.SnoozeEnabled)
        {
            SnoozeButton.Visibility = Visibility.Collapsed;
            GapCol.Width = new GridLength(0);
            Grid.SetColumn(DoneButton, 0);
            Grid.SetColumnSpan(DoneButton, 3);
        }
    }

    void Window_SourceInitialized(object? sender, EventArgs e)
    {
        PositionAtBottomRight();
        Open.Add(this);
        Closed += (_, _) => { Open.Remove(this); RestackOthers(); };
    }

    void PositionAtBottomRight()
    {
        var source = PresentationSource.FromVisual(this);
        var transformFromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;

        var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var workArea = screen.WorkingArea; // physical pixels

        var bottomRightDip = transformFromDevice.Transform(new System.Windows.Point(workArea.Right, workArea.Bottom));

        Left = bottomRightDip.X - Width - 16;
        Top = bottomRightDip.Y - ActualHeight - 16 - StackedOffset();
    }

    double StackedOffset() => Open.Sum(w => w.ActualHeight + Gap);

    static void RestackOthers()
    {
        double runningOffset = 0;
        foreach (var w in Open.AsEnumerable().Reverse())
        {
            var source = PresentationSource.FromVisual(w);
            var transformFromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var bottomRightDip = transformFromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

            w.Left = bottomRightDip.X - w.Width - 16;
            w.Top = bottomRightDip.Y - w.ActualHeight - 16 - runningOffset;
            runningOffset += w.ActualHeight + Gap;
        }
    }

    void Snooze_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var minutes in new[] { 5, 10, 30, 60 })
        {
            var item = new MenuItem { Header = $"{minutes} minutes" };
            item.Click += (_, _) => { _alerts.HandleAction(_reminderId, "snooze", minutes); Close(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    void Done_Click(object sender, RoutedEventArgs e)
    {
        _alerts.HandleAction(_reminderId, "done", null);
        Close();
    }

    void Dismiss_Click(object sender, RoutedEventArgs e) => Close();

    public static void DismissFor(Guid reminderId)
    {
        foreach (var w in Open.Where(w => w._reminderId == reminderId).ToList())
            w.Close();
    }
}
