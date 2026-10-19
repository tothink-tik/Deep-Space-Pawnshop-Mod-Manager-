using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Dangpu.Services;

public sealed class GameInfo : ObservableObject
{
    public string? GameDir { get; set; }
    public string? ExePath { get; set; }
    public string NameZh { get; set; } = "深空当铺：可能是偷的";
    public string NameEn { get; set; } = "Probably Stolen";
    public string Version { get; set; } = "";
    public string Loader { get; set; } = "";
    public string UnityVersion { get; set; } = "";
    public string GameType { get; set; } = "";
    public string GameArch { get; set; } = "x64";
    public string GameDeveloper { get; set; } = "";
    public string BuildId { get; set; } = "";
    public DateTime? BuildUpdated { get; set; }

    public string BuildText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(BuildId))
                return "Steam build " + BuildId + (BuildUpdated is { } d ? $" · 更新于 {d:yyyy-MM-dd HH:mm}" : "");
            if (BuildUpdated is { } fileTime)
                return $"游戏文件更新于 {fileTime:yyyy-MM-dd HH:mm}";
            return "";
        }
    }

    public void NotifyMetadataChanged()
    {
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(Loader));
        OnPropertyChanged(nameof(UnityVersion));
        OnPropertyChanged(nameof(GameType));
        OnPropertyChanged(nameof(GameArch));
        OnPropertyChanged(nameof(GameDeveloper));
        OnPropertyChanged(nameof(NameEn));
        OnPropertyChanged(nameof(BuildId));
        OnPropertyChanged(nameof(BuildUpdated));
        OnPropertyChanged(nameof(BuildText));
        OnPropertyChanged(nameof(VersionLine));
    }

    public bool Found => !string.IsNullOrWhiteSpace(GameDir) && Directory.Exists(GameDir) && File.Exists(ExePath ?? "");

    public string ModsDir => string.IsNullOrWhiteSpace(GameDir) ? "" : Path.Combine(GameDir!, "Mods");
    public string UserDataDir => string.IsNullOrWhiteSpace(GameDir) ? "" : Path.Combine(GameDir!, "UserData");
    public string LatestLog => string.IsNullOrWhiteSpace(GameDir) ? "" : Path.Combine(GameDir!, "MelonLoader", "Latest.log");
    public string ConfigPath => string.IsNullOrWhiteSpace(GameDir) ? "" : Path.Combine(GameDir!, "UserData", "MelonPreferences.cfg");
    public string LoaderCfgPath => string.IsNullOrWhiteSpace(GameDir) ? "" : Path.Combine(GameDir!, "UserData", "Loader.cfg");

    public string VersionLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Version)) parts.Add(Version);
            if (!string.IsNullOrWhiteSpace(Loader)) parts.Add(Loader);
            if (!string.IsNullOrWhiteSpace(GameType)) parts.Add(GameType.Equals("Il2cpp", StringComparison.OrdinalIgnoreCase) ? "IL2CPP" : GameType);
            return string.Join("  ·  ", parts);
        }
    }
}

public static class GameLocator
{
    public static IEnumerable<string> Candidates(string? saved)
    {
        if (!string.IsNullOrWhiteSpace(saved)) yield return saved!;

        foreach (var lib in SteamLibraries())
        {
            yield return Path.Combine(lib, "steamapps", "common", "Probably Stolen Demo");
            yield return Path.Combine(lib, "steamapps", "common", "Probably Stolen");
        }

        yield return @"C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo";
        yield return @"C:\Program Files\Steam\steamapps\common\Probably Stolen Demo";
    }

    public static string? Find(string? saved)
    {
        foreach (var dir in Candidates(saved).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Probably Stolen.exe")))
                    return Path.GetFullPath(dir);
            }
            catch { }
        }
        return null;
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var roots = new List<string>();
        foreach (var (hive, key) in new[]
                 {
                     (Registry.CurrentUser, @"Software\Valve\Steam"),
                     (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
                     (Registry.LocalMachine, @"SOFTWARE\Valve\Steam")
                 })
        {
            try
            {
                using var k = hive.OpenSubKey(key);
                var p = k?.GetValue("SteamPath") as string ?? k?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p)) roots.Add(p!);
            }
            catch { }
        }

        if (roots.Count == 0) roots.Add(@"C:\Program Files (x86)\Steam");

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            result.Add(root);
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            try
            {
                foreach (var line in File.ReadLines(vdf))
                {
                    var idx = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    var parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var p = parts[^1].Replace("\\\\", "\\");
                        if (Directory.Exists(p)) result.Add(p);
                    }
                }
            }
            catch { }
        }
        return result;
    }

    public static GameInfo Load(string? saved)
    {
        var info = new GameInfo();
        var dir = Find(saved);
        if (dir == null) return info;

        info.GameDir = dir;
        info.ExePath = Path.Combine(dir, "Probably Stolen.exe");

        var log = LogService.Read(Path.Combine(dir, "MelonLoader", "Latest.log"), 200);
        info.Version = log.Header.GetValueOrDefault("Game Version", "");
        info.Loader = log.Header.GetValueOrDefault("MelonLoader", "");
        info.UnityVersion = log.Header.GetValueOrDefault("Unity Version", "");
        info.GameType = log.Header.GetValueOrDefault("Game Type", "");
        info.GameArch = log.Header.GetValueOrDefault("Game Arch", "x64");
        info.GameDeveloper = log.Header.GetValueOrDefault("Game Developer", "");
        if (!string.IsNullOrWhiteSpace(log.Header.GetValueOrDefault("Game Name", "")))
            info.NameEn = log.Header["Game Name"];

        if (string.IsNullOrWhiteSpace(info.Version))
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(info.ExePath);
                info.Version = vi.FileVersion ?? "";
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(info.Loader))
        {
            try
            {
                var ml = Path.Combine(dir, "MelonLoader", "net6", "MelonLoader.dll");
                if (File.Exists(ml))
                {
                    var v = FileVersionInfo.GetVersionInfo(ml);
                    info.Loader = "MelonLoader " + (v.FileVersion?.Split('+')[0] ?? "0.7.3");
                }
            }
            catch { }
        }

        RefreshMetadata(info);
        return info;
    }

    /// <summary>重新读取日志头、Steam appmanifest 和 exe 时间；返回是否有变化。</summary>
    public static bool RefreshMetadata(GameInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.GameDir)) return false;

        var oldVersion = info.Version;
        var oldLoader = info.Loader;
        var oldUnity = info.UnityVersion;
        var oldType = info.GameType;
        var oldArch = info.GameArch;
        var oldDev = info.GameDeveloper;
        var oldName = info.NameEn;
        var oldBuild = info.BuildId;
        var oldUpdated = info.BuildUpdated;

        if (File.Exists(info.LatestLog))
        {
            var log = LogService.Read(info.LatestLog, 200);
            info.Version = HeaderOr(log, "Game Version", info.Version);
            info.Loader = HeaderOr(log, "MelonLoader", info.Loader);
            info.UnityVersion = HeaderOr(log, "Unity Version", info.UnityVersion);
            info.GameType = HeaderOr(log, "Game Type", info.GameType);
            info.GameArch = HeaderOr(log, "Game Arch", info.GameArch);
            info.GameDeveloper = HeaderOr(log, "Game Developer", info.GameDeveloper);
            info.NameEn = HeaderOr(log, "Game Name", info.NameEn);
        }

        var steam = ReadSteamBuild(info.GameDir);
        if (steam is { } build)
        {
            info.BuildId = build.Id;
            info.BuildUpdated = build.Updated;
        }
        else
        {
            info.BuildId = "";
            info.BuildUpdated = LatestGameFileTime(info.GameDir);
        }

        var changed = oldVersion != info.Version || oldLoader != info.Loader || oldUnity != info.UnityVersion ||
                      oldType != info.GameType || oldArch != info.GameArch || oldDev != info.GameDeveloper ||
                      oldName != info.NameEn || oldBuild != info.BuildId || oldUpdated != info.BuildUpdated;

        if (changed) info.NotifyMetadataChanged();
        return changed;
    }

    private static string HeaderOr(LogSnapshot log, string key, string fallback)
        => log.Header.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static (string Id, DateTime? Updated)? ReadSteamBuild(string dir)
    {
        try
        {
            var common = Directory.GetParent(dir);
            var steamapps = common?.Parent;
            if (steamapps == null || !steamapps.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                return null;

            foreach (var manifest in Directory.GetFiles(steamapps.FullName, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(manifest);
                if (!text.Contains("Probably Stolen", StringComparison.OrdinalIgnoreCase)) continue;

                var build = Regex.Match(text, "\"buildid\"\\s+\"(\\d+)\"");
                if (!build.Success) continue;

                DateTime? updated = null;
                var stamp = Regex.Match(text, "\"LastUpdated\"\\s+\"(\\d+)\"");
                if (stamp.Success && long.TryParse(stamp.Groups[1].Value, out var unix))
                    updated = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().DateTime;

                return (build.Groups[1].Value, updated);
            }
        }
        catch { }
        return null;
    }

    private static DateTime? LatestGameFileTime(string dir)
    {
        try
        {
            var times = new List<DateTime>();
            foreach (var name in new[] { "Probably Stolen.exe", "GameAssembly.dll" })
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path)) times.Add(File.GetLastWriteTime(path));
            }
            return times.Count == 0 ? null : times.Max();
        }
        catch { return null; }
    }
}

public static class GameRunner
{
    public static bool IsRunning()
    {
        try
        {
            return Process.GetProcessesByName("Probably Stolen").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool Launch(GameInfo game, AppSettings settings, out string error)
        => Launch(game, settings, false, out error);

    public static bool LaunchNoMods(GameInfo game, AppSettings settings, out string error)
        => Launch(game, settings, true, out error);

    private static bool Launch(GameInfo game, AppSettings settings, bool forceNoMods, out string error)
    {
        error = "";
        if (!game.Found || game.ExePath == null)
        {
            error = "还没有找到游戏目录";
            return false;
        }

        if (IsRunning())
        {
            error = "游戏已经在运行了";
            return false;
        }

        var args = new List<string>();
        if (settings.HideConsole) args.Add("--melonloader.hideconsole");
        if (settings.DebugMode) args.Add("--melonloader.debug");
        if (settings.CapturePlayerLogs) args.Add("--melonloader.captureplayerlogs");
        if (settings.NoMods || forceNoMods) args.Add("--no-mods");
        if (!string.IsNullOrWhiteSpace(settings.ExtraArgs)) args.Add(settings.ExtraArgs);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = game.ExePath,
                WorkingDirectory = game.GameDir!,
                UseShellExecute = true,
                Arguments = string.Join(" ", args)
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void OpenInExplorer(string? path, bool select = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe",
                    select ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"") { UseShellExecute = true });
            }
        }
        catch { }
    }
}
