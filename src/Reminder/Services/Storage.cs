using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReminderApp.Models;

namespace ReminderApp.Services;

public static class Storage
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    static readonly string RootDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Reminder");

    static readonly string RemindersPath = Path.Combine(RootDir, "reminders.json");
    static readonly string SettingsPath = Path.Combine(RootDir, "settings.json");

    public static ReminderFile LoadReminders() =>
        LoadWithFallback(RemindersPath, () => new ReminderFile());

    public static void SaveReminders(ReminderFile file) => SaveAtomic(RemindersPath, file);

    public static AppSettings LoadSettings() =>
        LoadWithFallback(SettingsPath, () => new AppSettings());

    public static void SaveSettings(AppSettings settings) => SaveAtomic(SettingsPath, settings);

    static T LoadWithFallback<T>(string path, Func<T> makeDefault) where T : class
    {
        Directory.CreateDirectory(RootDir);

        if (TryLoad<T>(path, out var value)) return value!;

        var backup = path + ".bak";
        if (TryLoad<T>(backup, out var fromBackup)) return fromBackup!;

        if (File.Exists(path))
        {
            var quarantine = path.Replace(".json", $".corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
            try { File.Move(path, quarantine, overwrite: true); } catch { /* best effort */ }
        }

        return makeDefault();
    }

    static bool TryLoad<T>(string path, out T? value) where T : class
    {
        value = null;
        if (!File.Exists(path)) return false;

        try
        {
            var text = File.ReadAllText(path);
            value = JsonSerializer.Deserialize<T>(text, Json);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    static void SaveAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(RootDir);

        var tmp = path + ".tmp";
        var backup = path + ".bak";

        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));

        if (File.Exists(path))
            File.Replace(tmp, path, backup);
        else
            File.Move(tmp, path);
    }
}
