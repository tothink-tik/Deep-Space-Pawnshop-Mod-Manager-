using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Dangpu.Services;

public enum LogLevel { Info, Good, Warn, Error, Muted }

public sealed class LogLine
{
    public string Time { get; init; } = "";
    public string Text { get; init; } = "";
    public LogLevel Level { get; init; }
    public string Raw { get; init; } = "";

    public Brush Fg => Level switch
    {
        LogLevel.Error => Frozen(0xEF, 0xA0, 0x93),
        LogLevel.Warn => Frozen(0xE4, 0xB8, 0x78),
        LogLevel.Good => Frozen(0x8F, 0xD3, 0xB0),
        LogLevel.Muted => Frozen(0x7C, 0x76, 0x6D),
        _ => Frozen(0xCF, 0xC8, 0xBB)
    };

    public Brush Bg => Level switch
    {
        LogLevel.Error => Frozen(0x2A, 0x1C, 0x1A),
        LogLevel.Warn => Frozen(0x2A, 0x24, 0x18),
        LogLevel.Good => Frozen(0x1C, 0x2A, 0x23),
        _ => Brushes.Transparent
    };

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}

public sealed class LogSnapshot
{
    public string Path { get; init; } = "";
    public bool Exists { get; set; }
    public Dictionary<string, string> Header { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<LogLine> Lines { get; } = new();
    public HashSet<string> LoadedDlls { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> FailedDlls { get; } = new();
    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }
    public int TotalLineCount { get; set; }
}

public static class LogService
{
    private static readonly Regex TimeRegex = new(@"^\[(\d{2}:\d{2}:\d{2}\.\d{3})\]\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex LoadedRegex = new(@"Melon Assembly loaded:\s*'([^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FailedRegex = new(@"Failed to load Melon Assembly from '([^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static LogSnapshot Read(string path, int maxLines = 600)
    {
        var snap = new LogSnapshot { Path = path };
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return snap;
        snap.Exists = true;

        string text;
        string head;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            // 头部（版本、加载器）永远在最前面，日志写大了也不能只读尾巴
            var headLength = (int)Math.Min(fs.Length, 16_384);
            var headBuffer = new byte[headLength];
            fs.ReadExactly(headBuffer, 0, headLength);
            head = Encoding.UTF8.GetString(headBuffer);

            var start = Math.Max(0, fs.Length - 500_000);
            fs.Seek(start, SeekOrigin.Begin);
            using var sr = new StreamReader(fs, Encoding.UTF8, true);
            text = sr.ReadToEnd();
            if (start > 0)
            {
                var nl = text.IndexOf('\n');
                if (nl >= 0) text = text[(nl + 1)..];
            }
        }
        catch
        {
            return snap;
        }

        ParseHeader(head, snap);

        var all = text.Replace("\r\n", "\n").Split('\n');
        snap.TotalLineCount = all.Length;
        var parsed = new List<LogLine>(all.Length);

        foreach (var raw in all)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var m = TimeRegex.Match(raw);
            var time = m.Success ? m.Groups[1].Value[..8] : "";
            var body = m.Success ? m.Groups[2].Value : raw;

            var lm = LoadedRegex.Match(body);
            if (lm.Success) snap.LoadedDlls.Add(FileNameOf(lm.Groups[1].Value));

            var fm = FailedRegex.Match(body);
            if (fm.Success) snap.FailedDlls.Add(FileNameOf(fm.Groups[1].Value));

            var level = Classify(body);
            if (level == LogLevel.Error) snap.ErrorCount++;
            if (level == LogLevel.Warn) snap.WarnCount++;

            parsed.Add(new LogLine { Time = time, Text = body, Level = level, Raw = raw });
        }

        if (parsed.Count > maxLines)
            parsed = parsed.Skip(parsed.Count - maxLines).ToList();

        snap.Lines.AddRange(parsed);
        return snap;
    }

    private static void ParseHeader(string text, LogSnapshot snap)
    {
        var ml = Regex.Match(text, @"MelonLoader v([\d.]+[^\s]*)", RegexOptions.IgnoreCase);
        if (ml.Success) snap.Header["MelonLoader"] = "MelonLoader " + ml.Groups[1].Value;

        foreach (var key in new[] { "Game Version", "Game Name", "Game Developer", "Unity Version", "Game Type", "Runtime Type", "Game Arch" })
        {
            var m = Regex.Match(text, Regex.Escape(key) + @":\s*(.+)");
            if (m.Success) snap.Header[key] = m.Groups[1].Value.Trim();
        }
    }

    private static LogLevel Classify(string body)
    {
        if (body.Contains("Melon Assembly loaded", StringComparison.OrdinalIgnoreCase)) return LogLevel.Good;
        if (body.Contains("Failed to load Melon Assembly", StringComparison.OrdinalIgnoreCase)) return LogLevel.Error;
        if (body.Contains("is incompatible", StringComparison.OrdinalIgnoreCase)) return LogLevel.Error;
        if (body.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)) return LogLevel.Error;
        if (body.Contains("Exception", StringComparison.Ordinal)) return LogLevel.Error;
        if (body.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase)) return LogLevel.Warn;
        if (body.Contains("[Warn]", StringComparison.OrdinalIgnoreCase)) return LogLevel.Warn;
        if (body.Contains("Il2CppAssemblyGenerator", StringComparison.OrdinalIgnoreCase)) return LogLevel.Muted;
        if (body.Contains("Contacting RemoteAPI", StringComparison.OrdinalIgnoreCase)) return LogLevel.Muted;
        return LogLevel.Info;
    }

    private static string FileNameOf(string p)
    {
        var s = p.Replace('/', '\\');
        var idx = s.LastIndexOf('\\');
        return idx >= 0 ? s[(idx + 1)..] : s;
    }

    public static void ApplyLoadStates(IEnumerable<Models.ModItem> mods, LogSnapshot snap)
    {
        foreach (var mod in mods)
        {
            var names = mod.DllNames.Count > 0
                ? mod.DllNames.Select(n => n.Replace(ModScanner.DisabledSuffix, "", StringComparison.OrdinalIgnoreCase)).ToList()
                : new List<string> { mod.FileName };

            if (names.Any(n => snap.FailedDlls.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                mod.LogState = "failed";
                continue;
            }

            if (names.Any(n => snap.LoadedDlls.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                mod.LogState = "loaded";
                continue;
            }

            mod.LogState = snap.Exists && snap.TotalLineCount > 30 ? "absent" : "";
        }
    }
}
