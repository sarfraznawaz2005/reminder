using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace ReminderApp.Services;

public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;

    public event Action? OpenRequested;
    public event Action? AddRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Add reminder", null, (_, _) => AddRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Reminder",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    static Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/app.ico");
        var info = Application.GetResourceStream(uri);
        return info is not null
            ? new Icon(info.Stream, SystemInformation.SmallIconSize)
            : SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
