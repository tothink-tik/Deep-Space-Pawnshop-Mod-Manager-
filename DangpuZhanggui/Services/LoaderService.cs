using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Dangpu.Services;

public sealed class LoaderStatus
{
    public bool GameFound { get; init; }
    public bool BootstrapExists { get; init; }
    public bool FolderExists { get; init; }
    public bool Installed => BootstrapExists && FolderExists;
    public string Version { get; init; } = "";
    public bool Disabled { get; init; }
    public bool AssembliesGenerated { get; init; }
    public string GameType { get; init; } = "";
    public bool CacheExists { get; init; }
    public long CacheSize { get; init; }

    public string Summary
    {
        get
        {
            if (!GameFound) return "先指定游戏目录";
            if (!Installed) return "没有安装 MelonLoader";
            var parts = new List<string> { string.IsNullOrWhiteSpace(Version) ? "已安装（版本未知）" : "已安装 " + Version };
            parts.Add(Disabled ? "当前被停用" : "已启用");
            if (GameType.Equals("Il2cpp", StringComparison.OrdinalIgnoreCase))
                parts.Add(AssembliesGenerated ? "IL2CPP 程序集已生成" : "还没有生成 IL2CPP 程序集");
            return string.Join("  ·  ", parts);
        }
    }
}

public readonly record struct LoaderProgress(long Received, long Total, string Phase)
{
    public double Percent => Total > 0 ? Math.Clamp(Received * 100.0 / Total, 0, 100) : 0;

    public string Text => Phase switch
    {
        "download" when Total > 0 => $"正在下载 {Received / 1024.0 / 1024.0:0.0} / {Total / 1024.0 / 1024.0:0.0} MB",
        "download" => $"正在下载 {Received / 1024.0 / 1024.0:0.0} MB",
        _ => Phase
    };
}

public static class LoaderService
{
    private const string FallbackUrl = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip";
    private const string FallbackTag = "v0.7.3";

    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "loader");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CachedZipPath => Path.Combine(CacheDir, "MelonLoader.x64.zip");

    /// <summary>放在 exe 同目录的安装包，用来做完全离线的绿色版。</summary>
    public static string PortableZipPath => Path.Combine(AppContext.BaseDirectory, "MelonLoader.x64.zip");

    public static string? ExistingZip =>
        File.Exists(PortableZipPath) && SafeZip(PortableZipPath) ? PortableZipPath
        : File.Exists(CachedZipPath) && SafeZip(CachedZipPath) ? CachedZipPath
        : null;

    public static bool HasCachedZip => ExistingZip != null;

    public static LoaderStatus GetStatus(GameInfo game)
    {
        if (!game.Found || string.IsNullOrWhiteSpace(game.GameDir))
            return new LoaderStatus { GameFound = false };

        var root = game.GameDir!;
        var bootstrap = Path.Combine(root, "version.dll");
        var folder = Path.Combine(root, "MelonLoader");
        var assemblies = Path.Combine(folder, "Il2CppAssemblies");

        bool assembliesGenerated = false;
        try
        {
            assembliesGenerated = Directory.Exists(assemblies) &&
                                  Directory.EnumerateFiles(assemblies, "*.dll").Any();
        }
        catch { }

        return new LoaderStatus
        {
            GameFound = true,
            BootstrapExists = File.Exists(bootstrap),
            FolderExists = Directory.Exists(folder),
            Version = ReadVersion(folder),
            Disabled = ReadDisabled(game),
            AssembliesGenerated = assembliesGenerated,
            GameType = game.GameType,
            CacheExists = ExistingZip != null,
            CacheSize = ExistingZip != null ? new FileInfo(ExistingZip).Length : 0
        };
    }

    private static string ReadVersion(string melonFolder)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(melonFolder, "net6", "MelonLoader.dll"),
                     Path.Combine(melonFolder, "net35", "MelonLoader.dll"),
                     Path.Combine(melonFolder, "MelonLoader.dll")
                 })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var v = FileVersionInfo.GetVersionInfo(candidate).FileVersion;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    var parts = v.Split('.').Take(3);
                    return "v" + string.Join(".", parts);
                }
            }
            catch { }
        }
        return "";
    }

    private static bool ReadDisabled(GameInfo game)
    {
        try
        {
            var cfg = game.LoaderCfgPath;
            if (!File.Exists(cfg)) return false;
            var inLoader = false;
            foreach (var line in File.ReadLines(cfg))
            {
                var t = line.Trim();
                if (t.StartsWith('[') && t.EndsWith(']'))
                {
                    inLoader = t[1..^1].Trim().Equals("loader", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inLoader) continue;
                var eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (t[..eq].Trim().Equals("disable", StringComparison.OrdinalIgnoreCase))
                    return t[(eq + 1)..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }

    public static (bool ok, string message) SetEnabled(GameInfo game, bool enabled)
    {
        var cfg = game.LoaderCfgPath;
        if (string.IsNullOrWhiteSpace(cfg)) return (false, "没有游戏目录");

        try
        {
            if (!File.Exists(cfg))
                return (false, "没有找到 UserData/Loader.cfg，先启动一次游戏让它生成");

            var lines = File.ReadAllLines(cfg).ToList();
            var inLoader = false;
            var changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (t.StartsWith('[') && t.EndsWith(']'))
                {
                    inLoader = t[1..^1].Trim().Equals("loader", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inLoader) continue;

                var eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (!t[..eq].Trim().Equals("disable", StringComparison.OrdinalIgnoreCase)) continue;

                var indent = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
                lines[i] = indent + "disable = " + (enabled ? "false" : "true");
                changed = true;
                break;
            }

            if (!changed) return (false, "Loader.cfg 里没有 [loader] disable 这一项");

            File.Copy(cfg, cfg + ".bak-dangpu-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true);
            File.WriteAllLines(cfg, lines);
            return (true, enabled ? "MelonLoader 已启用，下次启动生效" : "MelonLoader 已停用，下次启动就是纯净模式");
        }
        catch (Exception ex)
        {
            return (false, "改 Loader.cfg 失败：" + ex.Message);
        }
    }

    // ---------------- 下载 ----------------

    public static async Task<(string url, string tag)> ResolveLatestAsync(string arch, CancellationToken ct)
    {
        var asset = arch.Equals("x86", StringComparison.OrdinalIgnoreCase) ? "MelonLoader.x86.zip" : "MelonLoader.x64.zip";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DangpuZhanggui/1.0");
            var json = await client.GetStringAsync("https://api.github.com/repos/LavaGang/MelonLoader/releases/latest", ct);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.Equals(name, asset, StringComparison.OrdinalIgnoreCase)) continue;
                    var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(url)) return (url!, tag);
                }
            }
        }
        catch { }

        return (FallbackUrl, FallbackTag);
    }

    public static async Task<string> DownloadAsync(string url, string dest, IProgress<LoaderProgress>? progress,
        CancellationToken ct, int attempts = 4)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var part = dest + ".part";
        var partUrl = part + ".url";

        // 换了下载地址（比如发了新版本）就不能接着旧的半截文件续传
        if (File.Exists(part))
        {
            var previous = File.Exists(partUrl) ? File.ReadAllText(partUrl).Trim() : "";
            if (!string.Equals(previous, url, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(part); } catch { }
            }
        }
        try { File.WriteAllText(partUrl, url); } catch { }

        Exception? last = null;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DangpuZhanggui/1.0");

                long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                // 半截文件和远端对不上（比如换了版本），删掉从头来
                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    try { File.Delete(part); } catch { }
                    last = new IOException("断点位置无效，已清空重试");
                    if (attempt < attempts) await Task.Delay(600, ct);
                    continue;
                }

                if (existing > 0 && response.StatusCode == HttpStatusCode.OK)
                {
                    existing = 0;
                    File.Delete(part);
                }
                response.EnsureSuccessStatusCode();

                var total = (response.Content.Headers.ContentLength ?? 0) + existing;
                progress?.Report(new LoaderProgress(existing, total, "download"));

                await using (var file = new FileStream(part, existing > 0 ? FileMode.Append : FileMode.Create,
                                 FileAccess.Write, FileShare.None, 81920, true))
                await using (var stream = await response.Content.ReadAsStreamAsync(ct))
                {
                    var buffer = new byte[81920];
                    long received = existing;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), ct);
                        received += read;
                        progress?.Report(new LoaderProgress(received, total, "download"));
                    }
                }

                if (!SafeZip(part)) throw new InvalidDataException("下载到的文件不是完整的 zip，可能被网络截断了");

                File.Move(part, dest, true);
                try { File.Delete(partUrl); } catch { }
                return dest;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                // 校验不过的半截包不能再接着用，否则会一直坏下去
                try { File.Delete(part); } catch { }
                last = ex;
                if (attempt < attempts) await Task.Delay(1200 * attempt, ct);
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt < attempts) await Task.Delay(1200 * attempt, ct);
            }
        }

        throw new IOException("下载失败：" + (last?.Message ?? "未知网络错误") + "。可以重试，或者手动下载后用「从 zip 安装」。", last);
    }

    private static bool SafeZip(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 1024) return false;
            using var zip = ZipFile.OpenRead(path);
            return zip.Entries.Any(e => e.FullName.Equals("version.dll", StringComparison.OrdinalIgnoreCase))
                   && zip.Entries.Any(e => e.FullName.StartsWith("MelonLoader/", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    // ---------------- 安装 / 导出 ----------------

    public static void InstallFromZip(string zipPath, GameInfo game, string tag = "")
    {
        if (!game.Found || string.IsNullOrWhiteSpace(game.GameDir))
            throw new InvalidOperationException("没有找到游戏目录");

        if (!SafeZip(zipPath))
            throw new InvalidDataException("这个 zip 里没有 version.dll 和 MelonLoader 目录，不像是 MelonLoader 安装包");

        var root = Path.GetFullPath(game.GameDir!);
        var current = GetStatus(game);
        var targetSeries = Series(tag);
        var currentSeries = Series(current.Version);
        var sameSeries = targetSeries != "" && targetSeries == currentSeries;

        var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        var bootstrap = Path.Combine(root, "version.dll");
        if (File.Exists(bootstrap))
            File.Move(bootstrap, bootstrap + ".bak-" + stamp, true);

        var folder = Path.Combine(root, "MelonLoader");
        if (Directory.Exists(folder) && !sameSeries && !string.IsNullOrWhiteSpace(current.Version))
            Directory.Move(folder, folder + ".bak-" + stamp);

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var dest = Path.GetFullPath(Path.Combine(root, name));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("压缩包里有奇怪的路径：" + entry.FullName);

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, true);
        }

        if (!File.Exists(bootstrap) || !Directory.Exists(folder))
            throw new IOException("解压完了但文件不全，检查一下游戏目录的写入权限");
    }

    public static string ExportLoader(GameInfo game, string destZip)
    {
        if (!game.Found) throw new InvalidOperationException("没有找到游戏目录");

        var status = GetStatus(game);
        if (!status.Installed) throw new InvalidOperationException("当前没有安装 MelonLoader，没什么可导出的");

        var root = game.GameDir!;
        var bootstrap = Path.Combine(root, "version.dll");
        var folder = Path.Combine(root, "MelonLoader");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destZip))!);
        if (File.Exists(destZip)) File.Delete(destZip);

        using var zip = ZipFile.Open(destZip, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(bootstrap, "version.dll", CompressionLevel.Optimal);

        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (rel.StartsWith("Logs/", StringComparison.OrdinalIgnoreCase)) continue;
            if (rel.StartsWith("Il2CppAssemblies/", StringComparison.OrdinalIgnoreCase)) continue;
            if (rel.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) continue;

            zip.CreateEntryFromFile(file, "MelonLoader/" + rel, CompressionLevel.Optimal);
        }

        return destZip;
    }

    private static string Series(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "";
        var v = version.Trim().TrimStart('v', 'V');
        var parts = v.Split('.');
        if (parts.Length < 2) return "";
        return parts[0] + "." + parts[1];
    }
}
