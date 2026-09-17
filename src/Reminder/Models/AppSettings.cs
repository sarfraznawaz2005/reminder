namespace ReminderApp.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public bool StartWithWindows { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; }

    public int DefaultSnoozeMinutes { get; set; } = 10;

    // Clean clock-time default used when a new reminder doesn't specify one.
    public int DefaultHour { get; set; } = 9;
    public int DefaultMinute { get; set; } = 0;

    public WindowPlacement Window { get; set; } = new();
}

public sealed class WindowPlacement
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 980;
    public double Height { get; set; } = 620;
    public bool Maximized { get; set; }
}
