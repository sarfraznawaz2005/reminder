using System.Windows;
using ReminderApp.Models;
using ReminderApp.Services;
using ReminderApp.Views;

namespace ReminderApp;

public partial class App : System.Windows.Application
{
    public static bool IsExiting { get; private set; }

    SingleInstance? _singleInstance;
    TrayIcon? _tray;
    ReminderStore? _store;
    Scheduler? _scheduler;
    AlertService? _alerts;
    AppSettings? _settings;
    MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance();
        _singleInstance.Acquire();
        if (!_singleInstance.IsFirstInstance)
        {
            Shutdown(0);
            return;
        }

        bool startInTray = e.Args.Contains("--tray");

        _settings = Storage.LoadSettings();
        _store = new ReminderStore();
        _store.Load();

        _scheduler = new Scheduler(_store);

        _alerts = new AlertService(_store, _scheduler, _settings);
        _alerts.ShowPopupRequested += r => new AlertPopupWindow(r, _alerts).Show();
        _alerts.DismissPopupRequested += AlertPopupWindow.DismissFor;

        _mainWindow = new MainWindow(_store, _scheduler, _alerts, _settings);

        _tray = new TrayIcon();
        _tray.OpenRequested += () => _mainWindow.ShowAndActivate();
        _tray.AddRequested += () =>
        {
            _mainWindow.ShowAndActivate();
            _mainWindow.OpenAddNew();
        };
        _tray.SettingsRequested += () =>
        {
            var settingsWindow = new SettingsWindow(_settings) { Owner = _mainWindow };
            settingsWindow.ShowDialog();
        };
        _tray.ExitRequested += () => ExitApplication();
        _mainWindow.ExitRequested += () => ExitApplication();

        if (_settings.StartWithWindows) StartupRegistration.Apply(true);

        _singleInstance.ListenForActivation(() => Dispatcher.Invoke(() => _mainWindow.ShowAndActivate()));

        _scheduler.Start();

        if (!(startInTray || _settings.StartMinimized))
            _mainWindow.Show();
    }

    void ExitApplication()
    {
        IsExiting = true;
        _mainWindow?.SavePlacement();
        _store?.SaveNow();
        _scheduler?.Stop();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
