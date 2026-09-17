namespace ReminderApp.Models;

public sealed class ReminderFile
{
    public int Version { get; set; } = 1;
    public List<Reminder> Reminders { get; set; } = new();
}
