using System.Media;

namespace ReminderApp.Services;

// Windows' SystemSounds categories (Asterisk/Exclamation/Beep/...) are just labels for
// whatever the user's current sound scheme assigns them - on many machines several of them
// point at the same file, so "Chime" and "Default" can sound identical. Synthesizing short
// tones in memory instead makes each ringtone sound the same, and actually distinct, on
// every machine, with no bundled audio files and no new dependency.
public static class Ringtone
{
    const int SampleRate = 44100;

    public static void Play(string ringtone)
    {
        switch (ringtone)
        {
            case "Mute":
                break;
            case "Default":
                PlayTones((880, 150));
                break;
            case "Chime":
                PlayTones((659, 140), (988, 240));
                break;
            case "Alert":
                PlayTones((988, 90), (988, 90), (988, 90));
                break;
            default:
                if (IsLocalFilePath(ringtone) && File.Exists(ringtone))
                {
                    try { new SoundPlayer(ringtone).Play(); } catch { /* best effort */ }
                }
                break;
        }
    }

    // Ringtone can come from an imported reminders file, so reject network (UNC) paths here
    // to stop a crafted import from making the app touch a remote share when a reminder fires.
    static bool IsLocalFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
        return Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile && !uri.IsUnc;
    }

    static void PlayTones(params (int hz, int ms)[] tones)
    {
        using var stream = BuildWav(tones);
        var player = new SoundPlayer(stream);
        player.Load(); // buffers synchronously, so the stream can be disposed once this returns
        player.Play(); // then plays asynchronously from the buffered copy
    }

    static MemoryStream BuildWav((int hz, int ms)[] tones)
    {
        const int gapMs = 30;
        const double fadeMs = 6;

        var samples = new List<short>();
        for (int t = 0; t < tones.Length; t++)
        {
            var (hz, ms) = tones[t];
            int count = SampleRate * ms / 1000;
            int fadeCount = (int)(SampleRate * fadeMs / 1000);

            for (int i = 0; i < count; i++)
            {
                double amplitude = 0.4;
                if (i < fadeCount) amplitude *= i / (double)fadeCount;
                else if (i > count - fadeCount) amplitude *= (count - i) / (double)fadeCount;

                double v = Math.Sin(2 * Math.PI * hz * i / SampleRate) * amplitude;
                samples.Add((short)(v * short.MaxValue));
            }

            if (t < tones.Length - 1)
                samples.AddRange(new short[SampleRate * gapMs / 1000]);
        }

        var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            int dataBytes = samples.Count * 2;
            bw.Write("RIFF"u8.ToArray());
            bw.Write(36 + dataBytes);
            bw.Write("WAVE"u8.ToArray());
            bw.Write("fmt "u8.ToArray());
            bw.Write(16);
            bw.Write((short)1);           // PCM
            bw.Write((short)1);           // mono
            bw.Write(SampleRate);
            bw.Write(SampleRate * 2);     // byte rate (16-bit mono)
            bw.Write((short)2);           // block align
            bw.Write((short)16);          // bits per sample
            bw.Write("data"u8.ToArray());
            bw.Write(dataBytes);
            foreach (var s in samples) bw.Write(s);
        }

        stream.Position = 0;
        return stream;
    }
}
