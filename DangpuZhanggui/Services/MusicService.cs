using System.IO;
using System.Windows.Media;

namespace Dangpu.Services;

public static class MusicService
{
    private static readonly string[] Extensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac" };
    private static readonly MediaPlayer Player = new();
    private static readonly List<string> Tracks = new();
    private static int _index = -1;
    private static bool _isPlaying;

    public static event Action? StateChanged;

    public static bool Loop { get; set; } = true;
    public static bool IsPlaying => _isPlaying;
    public static string CurrentName => _index >= 0 && _index < Tracks.Count
        ? Path.GetFileNameWithoutExtension(Tracks[_index])
        : "";

    static MusicService()
    {
        Player.MediaEnded += (_, _) =>
        {
            if (Tracks.Count == 0) return;
            if (Loop) Play((_index + 1) % Tracks.Count);
            else if (_index + 1 < Tracks.Count) Play(_index + 1);
            else Stop();
        };

        Player.MediaFailed += (_, _) =>
        {
            _isPlaying = false;
            Notify();
        };
    }

    public static string Dir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "music");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static bool IsSupported(string path)
        => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static string? Import(string source)
    {
        try
        {
            if (!File.Exists(source) || !IsSupported(source)) return null;

            var name = Path.GetFileNameWithoutExtension(source);
            var ext = Path.GetExtension(source).ToLowerInvariant();
            var dest = Path.Combine(Dir, name + ext);
            var n = 2;
            while (File.Exists(dest))
                dest = Path.Combine(Dir, $"{name} ({n++}){ext}");

            File.Copy(source, dest);
            return dest;
        }
        catch
        {
            return null;
        }
    }

    public static void Load(IEnumerable<string> paths, double volume, bool loop)
    {
        Stop();
        Tracks.Clear();
        Tracks.AddRange(paths.Where(File.Exists));
        Loop = loop;
        SetVolume(volume);
        Notify();
    }

    public static void Play(int index)
    {
        if (index < 0 || index >= Tracks.Count) return;

        try
        {
            Player.Open(new Uri(Tracks[index]));
            Player.Play();
            _index = index;
            _isPlaying = true;
        }
        catch
        {
            _isPlaying = false;
        }

        Notify();
    }

    public static void Toggle()
    {
        if (_isPlaying)
        {
            Player.Pause();
            _isPlaying = false;
        }
        else if (_index >= 0 && _index < Tracks.Count)
        {
            Player.Play();
            _isPlaying = true;
        }
        else if (Tracks.Count > 0)
        {
            Play(0);
            return;
        }

        Notify();
    }

    public static void Next()
    {
        if (Tracks.Count == 0) return;
        Play((_index + 1 + Tracks.Count) % Tracks.Count);
    }

    public static void Previous()
    {
        if (Tracks.Count == 0) return;
        Play((_index - 1 + Tracks.Count) % Tracks.Count);
    }

    public static void Stop()
    {
        try { Player.Stop(); } catch { }
        _isPlaying = false;
        _index = -1;
        Notify();
    }

    public static void SetVolume(double volume)
    {
        try { Player.Volume = Math.Clamp(volume, 0, 1); } catch { }
        Notify();
    }

    public static void DeleteFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try { File.Delete(path); } catch { }
        }
    }

    public static void Shutdown()
    {
        try { Player.Close(); } catch { }
        _isPlaying = false;
    }

    private static void Notify() => StateChanged?.Invoke();
}
