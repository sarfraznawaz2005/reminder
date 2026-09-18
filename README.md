# Reminder

A simple, clean, offline reminder app for Windows, built with WPF on .NET 8.

## Features

- Once, hourly, daily, weekly, monthly (multiple days, e.g. 29/30/31), and yearly reminders,
  all anchored to wall-clock time - "daily at 9:00 AM" always means 9:00 AM local time, not
  "24 hours from when you saved it".
- Daily/weekly/monthly/yearly reminders can fire at more than one time of day - e.g. one
  "Take medicine" reminder at 8 AM, 1 PM, and 8 PM, instead of three separate reminders.
- Pause a reminder without deleting or completing it - it stays in the list, just stops firing
  until you resume it.
- Reminders survive daylight-saving-time transitions correctly: a reminder that lands in the
  spring-forward gap fires once at the end of the gap, and a reminder in the repeated fall-back
  hour fires exactly once.
- Alerts via an in-app popup, with Snooze (5/10/30/60 minutes, or turned off entirely) and Done.
- Runs from the system tray. Start with Windows, close to tray, minimize to tray, start
  minimized - all optional, all in Settings.
- Select multiple reminders to delete or mark complete in one go.
- Export all reminders to a `.json` file and import them back in - import always adds, never
  overwrites or removes what's already there.
- Sharp at any display scale (tested at 250%).
- If the app was closed when a reminder was due, it is skipped quietly and rolls forward to its
  next occurrence - no backlog of stale alerts on relaunch.

## What this app does not do

- No network access of any kind. Nothing is sent anywhere, ever.
- No telemetry, analytics, or update checks.
- No email or notification-service integration.
- Does not wake your PC from sleep to fire a reminder that was due while it was asleep - it
  fires the next occurrence quietly instead once you're back.

## Data and privacy

All data stays on your machine, under your own user account:

- Reminders and settings: `%APPDATA%\Reminder\reminders.json` and `settings.json`, plain JSON,
  human-readable.
- "Start with Windows" writes one value under
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. No admin rights are used
  or required, and nothing is written to `HKEY_LOCAL_MACHINE`.

## Building

Requires the .NET 8 SDK (`dotnet --version` should report 8.x).

- `build.bat` (or `build.ps1`) - cleans, then publishes a slim, framework-dependent,
  single-file production exe to `publish\Reminder.exe`. "Framework-dependent" means it needs
  the .NET 8 Desktop Runtime already on the machine that runs it - that's what keeps it small
  (~3 MB) instead of bundling the whole runtime.
- `run.bat` (or `run.ps1`) - cleans, then runs the app straight from source in dev mode
  (`dotnet run`, Debug config).

Both scripts clean `bin`/`obj` before doing anything else, so each run starts from a known
state.

## Testing

```
dotnet test tests/Reminder.Tests/Reminder.Tests.csproj
```

The test suite covers the recurrence and daylight-saving-time math specifically - the part of
the app that's effectively impossible to verify by hand, since the bugs it catches only
reproduce twice a year.

## Project layout

```
src/Reminder/       the app (WPF, .NET 8)
  Models/            Reminder, RecurrenceRule, AppSettings - what's persisted
  Services/          storage, the recurrence calculator, the scheduler, tray, alerts
  Views/             MainWindow, the add/edit dialog, Settings, the alert popup
tests/Reminder.Tests/  xunit tests for the recurrence/DST math
```
