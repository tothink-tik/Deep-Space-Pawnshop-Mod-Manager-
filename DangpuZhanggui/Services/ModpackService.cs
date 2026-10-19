using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dangpu.Models;

namespace Dangpu.Services;

public static class ModpackService
{
    public const int CurrentFormat = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string LibraryDir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "modpacks");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static ModpackInfo Create(GameInfo game, IReadOnlyList<ModItem> mods,
        string name, string version, string author, string description, bool includeConfig, string outputPath)
    {
        if (mods.Count == 0) throw new InvalidOperationException("没有可打包的已启用模组");
        if (string.IsNullOrWhiteSpace(name)) name = "Probably Stolen 整合包";
        if (string.IsNullOrWhiteSpace(version)) version = "1.0";

        var staging = Path.Combine(Path.GetTempPath(), "dangpu-pack-" + Guid.NewGuid().ToString("N"));
        var modsStage = Path.Combine(staging, "mods");
        var configStage = Path.Combine(staging, "config");
        Directory.CreateDirectory(modsStage);

        try
        {
            var manifest = new ModpackManifest
            {
                Format = CurrentFormat,
                Name = name.Trim(),
                Version = version.Trim(),
                Author = author.Trim(),
                Description = description.Trim(),
                Created = DateTime.Now.ToString("O"),
                IncludeConfig = includeConfig,
                Game = new ModpackGame
                {
                    Name = game.NameEn,
                    Version = game.Version,
                    BuildId = game.BuildId,
                    Loader = game.Loader,
                    UnityVersion = game.UnityVersion
                }
            };

            foreach (var mod in mods)
            {
                var entryName = mod.IsFolder ? mod.FileName : Path.GetFileName(mod.Path);
                if (mod.IsFolder)
                    CopyDirectory(mod.Path, Path.Combine(modsStage, entryName));
                else
                    File.Copy(mod.Path, Path.Combine(modsStage, entryName), true);

                manifest.Mods.Add(new ModpackMod
                {
                    Entry = mod.IsFolder ? entryName + "/" : entryName,
                    Name = mod.DisplayName,
                    Version = mod.VersionText,
                    Author = mod.Author ?? "",
                    Enabled = mod.Enabled,
                    Sha256 = mod.IsFolder ? HashDirectory(mod.Path) : HashFile(mod.Path),
                    Size = mod.Size,
                    Source = mod.CatalogLink ?? mod.DownloadLink ?? ""
                });
            }

            var configApplied = false;
            if (includeConfig && File.Exists(game.ConfigPath))
            {
                Directory.CreateDirectory(configStage);
                File.Copy(game.ConfigPath, Path.Combine(configStage, "MelonPreferences.cfg"), true);
                configApplied = true;
            }

            File.WriteAllText(Path.Combine(staging, "modpack.json"),
                JsonSerializer.Serialize(manifest, JsonOptions), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(staging, "README.txt"), BuildReadme(manifest), new UTF8Encoding(false));

            var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(outDir)) Directory.CreateDirectory(outDir);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            ZipFile.CreateFromDirectory(staging, outputPath, CompressionLevel.Optimal, false);

            return Read(outputPath);
        }
        finally
        {
            try { Directory.Delete(staging, true); } catch { }
        }
    }

    public static ModpackInfo Read(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists) throw new FileNotFoundException("整合包不存在", path);

        using var zip = ZipFile.OpenRead(path);
        var entry = zip.Entries.FirstOrDefault(e =>
            e.FullName.Equals("modpack.json", StringComparison.OrdinalIgnoreCase));
        if (entry == null) throw new InvalidDataException("不是有效的整合包：缺少 modpack.json");

        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<ModpackManifest>(stream, JsonOptions)
                       ?? throw new InvalidDataException("modpack.json 读不出来");
        if (manifest.Format < 1 || manifest.Format > CurrentFormat)
            throw new InvalidDataException($"不支持的整合包格式：{manifest.Format}");
        manifest.Mods ??= new List<ModpackMod>();
        if (string.IsNullOrWhiteSpace(manifest.Name)) manifest.Name = Path.GetFileNameWithoutExtension(path);

        var created = DateTime.TryParse(manifest.Created, out var parsed) ? parsed : fi.LastWriteTime;
        return new ModpackInfo
        {
            Path = Path.GetFullPath(path),
            Manifest = manifest,
            FileSize = fi.Length,
            Created = created
        };
    }

    public static List<ModpackInfo> ListLibrary()
    {
        var list = new List<ModpackInfo>();
        try
        {
            foreach (var file in Directory.GetFiles(LibraryDir)
                         .Where(f => f.EndsWith(".dspmp", StringComparison.OrdinalIgnoreCase) ||
                                     f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    list.Add(Read(file));
                }
                catch (Exception ex)
                {
                    var fi = new FileInfo(file);
                    list.Add(new ModpackInfo
                    {
                        Path = file,
                        FileSize = fi.Length,
                        Created = fi.LastWriteTime,
                        Manifest = new ModpackManifest { Name = Path.GetFileNameWithoutExtension(file) },
                        Error = ex.Message
                    });
                }
            }
        }
        catch { }
        return list.OrderByDescending(p => p.Created).ToList();
    }

    public static ModpackInfo Import(string source)
    {
        var info = Read(source);
        Directory.CreateDirectory(LibraryDir);

        var baseName = Sanitize($"{info.Name}-{info.Version}");
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "modpack";
        var dest = Path.Combine(LibraryDir, baseName + ".dspmp");
        var n = 2;
        while (File.Exists(dest) && !string.Equals(Path.GetFullPath(dest), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
            dest = Path.Combine(LibraryDir, $"{baseName}-{n++}.dspmp");

        if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
            File.Copy(source, dest, true);

        return Read(dest);
    }

    public static ModpackInfo AddToLibrary(string source)
    {
        var info = Read(source);
        Directory.CreateDirectory(LibraryDir);
        var baseName = Sanitize($"{info.Name}-{info.Version}");
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "modpack";
        var dest = Path.Combine(LibraryDir, baseName + ".dspmp");
        var n = 2;
        while (File.Exists(dest) &&
               !string.Equals(Path.GetFullPath(dest), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
            dest = Path.Combine(LibraryDir, $"{baseName}-{n++}.dspmp");

        if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
            File.Copy(source, dest, true);
        return Read(dest);
    }

    public static ModpackInstallResult Install(GameInfo game, string packPath, bool checkRunning = true)
    {
        if (checkRunning && GameRunner.IsRunning()) throw new InvalidOperationException("游戏正在运行，先关掉再安装整合包");
        if (!game.Found) throw new InvalidOperationException("没有找到游戏目录");

        var info = Read(packPath);
        var manifest = info.Manifest;
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(manifest.Game.Version) &&
            !string.Equals(manifest.Game.Version, game.Version, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"整合包是 {manifest.Game.Version} 的，当前游戏是 {game.Version}");
        if (!string.IsNullOrWhiteSpace(manifest.Game.BuildId) &&
            !string.IsNullOrWhiteSpace(game.BuildId) &&
            !string.Equals(manifest.Game.BuildId, game.BuildId, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Steam build 不同：整合包 {manifest.Game.BuildId}，当前 {game.BuildId}");

        var backup = BackupService.Create(game, "modpack-" + Sanitize(info.Name));
        var modsDir = game.ModsDir;
        Directory.CreateDirectory(modsDir);

        foreach (var file in Directory.GetFiles(modsDir)) File.Delete(file);
        foreach (var dir in Directory.GetDirectories(modsDir)) Directory.Delete(dir, true);

        var modEntries = 0;
        var configApplied = false;

        using (var zip = ZipFile.OpenRead(packPath))
        {
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith("/", StringComparison.Ordinal)) continue;

                if (name.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = name["mods/".Length..];
                    if (string.IsNullOrWhiteSpace(rel)) continue;
                    var dest = SafeJoin(modsDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, true);
                    modEntries++;
                }
                else if (manifest.IncludeConfig &&
                         name.Equals("config/MelonPreferences.cfg", StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(game.ConfigPath)!);
                    entry.ExtractToFile(game.ConfigPath, true);
                    configApplied = true;
                }
            }
        }

        if (modEntries == 0) throw new InvalidDataException("整合包里没有模组文件");
        if (manifest.IncludeConfig && !configApplied) warnings.Add("整合包声明包含配置，但没有找到 MelonPreferences.cfg");

        return new ModpackInstallResult
        {
            BackupPath = backup.FullPath,
            ModCount = modEntries,
            ConfigApplied = configApplied,
            Warnings = warnings
        };
    }

    private static string SafeJoin(string root, string relative)
    {
        var rootFull = Path.GetFullPath(root);
        var dest = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!dest.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("整合包里有非法路径");
        return dest;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            if (Path.GetFileName(file).Contains(".old-", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            if (name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("bin", StringComparison.OrdinalIgnoreCase)) continue;
            CopyDirectory(dir, Path.Combine(dest, name));
        }
    }

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string HashDirectory(string dir)
    {
        using var sha = SHA256.Create();
        var lines = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
                rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).Contains(".old-", StringComparison.OrdinalIgnoreCase)) continue;
            lines.Add(rel + ":" + HashFile(file));
        }
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
    }

    private static string BuildReadme(ModpackManifest m)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{m.Name} {m.Version}");
        sb.AppendLine($"Author: {m.Author}");
        sb.AppendLine($"Game: {m.Game.Name} {m.Game.Version} (build {m.Game.BuildId})");
        sb.AppendLine($"Mods: {m.Mods.Count}");
        sb.AppendLine();
        foreach (var mod in m.Mods) sb.AppendLine($"- {mod.Name} {mod.Version}  [{mod.Entry}]");
        sb.AppendLine();
        sb.AppendLine("Install with Deep Space Pawnshop Mod Manager -> 整合包 -> 导入/安装.");
        return sb.ToString();
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }
}
