using System.Text.Json.Serialization;

namespace ReminderApp.Models;

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public RecurrenceRule Rule { get; set; } = new();

    // "Mute" | "Default" | "Chime" | "Alert" | absolute path to a .wav file
    public string Ringtone { get; set; } = "Default";

    public bool IsEnabled { get; set; } = true;
    public bool IsCompleted { get; set; }

    public DateTime? SnoozedUntil { get; set; }
    public DateTime? LastFired { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Runtime scheduler cursor. Recomputed from DateTime.Now on every load, never persisted.
    // Anything that elapsed while the app was closed is simply unreachable - this IS the
    // "skip missed reminders quietly" behavior; there is no catch-up path to disable.
    [JsonIgnore] public DateTime? NextLocal { get; set; }
    [JsonIgnore] public DateTimeOffset? NextInstantUtc { get; set; }
    [JsonIgnore] public DateTimeOffset? SnoozeInstantUtc { get; set; }
}
