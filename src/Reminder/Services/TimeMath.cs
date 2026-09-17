namespace ReminderApp.Services;

// The only layer that knows about timezones and DST. Takes TimeZoneInfo as a parameter
// (rather than reading TimeZoneInfo.Local internally) purely so tests can pin a fixed zone;
// production always passes TimeZoneInfo.Local.
public static class TimeMath
{
    const int MaxGapWalkMinutes = 180;

    public static DateTimeOffset ToInstant(DateTime localWallClock, TimeZoneInfo tz)
    {
        var c = localWallClock;

        if (tz.IsInvalidTime(c))
        {
            // Spring-forward gap (e.g. 2:30 AM doesn't exist): fire at the end of the gap.
            // Skipping the day instead would silently lose the reminder once a year.
            int walked = 0;
            while (tz.IsInvalidTime(c) && walked++ < MaxGapWalkMinutes)
                c = c.AddMinutes(1);
        }

        if (tz.IsAmbiguousTime(c))
        {
            // Fall-back (e.g. 2:30 AM happens twice): fire on the first pass only,
            // by taking the larger (earlier, DST) offset. Prevents a double-fire.
            var offsets = tz.GetAmbiguousTimeOffsets(c);
            var offset = offsets.Length > 0 ? offsets.Max() : tz.GetUtcOffset(c);
            return new DateTimeOffset(c, offset);
        }

        return new DateTimeOffset(c, tz.GetUtcOffset(c));
    }
}
