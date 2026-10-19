using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Dangpu.Services;

public sealed class BackupItem
{
    public string FullPath { get; init; } = "";
    public string Name => Path.GetFileNameWithoutExtension(FullPath);
    public DateTime Created { get; init; }
    public long Size { get; init; }
    public string SizeText => Size < 1024 * 1024 ? $"{Size / 1024.0:0.#} KB" : $"{Size / 1024.0 / 1024.0:0.##} MB";
    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm:ss");
    public string Title => Created.ToString("yyyy-MM-dd HH:mm");
    public string Label { get; init; } = "";
}

public static class BackupService
{
    public static string BackupDir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "backups");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static BackupItem Create(GameInfo game, string label)
    {
        if (!game.Found) throw new InvalidOperationException("没有找到游戏目录");

        var safeLabel = string.Concat(label.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        var name = $"{DateTime.Now:yyyyMMdd-HHmmss}-{safeLabel}.zip";
        var path = Path.Combine(BackupDir, name);

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var modsDir = game.ModsDir;
            if (Directory.Exists(modsDir))
            {
                foreach (var file in Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(modsDir, file).Replace('\\', '/');
                    if (rel.Contains(".old-", StringComparison.OrdinalIgnoreCase)) continue;
                    zip.CreateEntryFromFile(file, "Mods/" + rel, CompressionLevel.Optimal);
                }
            }

            foreach (var (src, entryName) in new[]
                     {
                         (game.ConfigPath, "UserData/MelonPreferences.cfg"),
                         (game.LoaderCfgPath, "UserData/Loader.cfg")
                     })
            {
                if (File.Exists(src)) zip.CreateEntryFromFile(src, entryName, CompressionLevel.Optimal);
            }

            var meta = zip.CreateEntry("dangpu-backup.json");
            using var w = new StreamWriter(meta.Open());
            w.Write(JsonSerializer.Serialize(new
            {
                created = DateTime.Now,
                game = game.GameDir,
                version = game.Version,
                mods = Directory.Exists(modsDir) ? Directory.GetFiles(modsDir, "*.dll", SearchOption.AllDirectories).Length : 0,
                label
            }));
        }

        var fi = new FileInfo(path);
        return new BackupItem { FullPath = path, Created = fi.LastWriteTime, Size = fi.Length, Label = label };
    }

    public static List<BackupItem> List()
    {
        var list = new List<BackupItem>();
        try
        {
            foreach (var file in Directory.GetFiles(BackupDir, "*.zip"))
            {
                var fi = new FileInfo(file);
                list.Add(new BackupItem
                {
                    FullPath = file,
                    Created = fi.LastWriteTime,
                    Size = fi.Length,
                    Label = LabelFrom(file)
                });
            }
        }
        catch { }
        return list.OrderByDescending(b => b.Created).ToList();
    }

    private static string LabelFrom(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var idx = name.IndexOf('-', 15);
        return idx > 0 && idx + 1 < name.Length ? name[(idx + 1)..] : "";
    }

    public static void Restore(BackupItem item, GameInfo game)
    {
        using var zip = ZipFile.OpenRead(item.FullPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue;
            if (entry.FullName.Equals("dangpu-backup.json", StringComparison.OrdinalIgnoreCase)) continue;

            string target;
            if (entry.FullName.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase))
                target = Path.Combine(game.ModsDir, entry.FullName[5..]);
            else if (entry.FullName.StartsWith("UserData/", StringComparison.OrdinalIgnoreCase))
                target = Path.Combine(game.UserDataDir, entry.FullName[9..]);
            else
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            EnsureUnder(target, entry.FullName.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase) ? game.ModsDir : game.UserDataDir);
            entry.ExtractToFile(target, true);
        }
    }

    private static void EnsureUnder(string target, string root)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(target).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("备份里有越界路径：" + target);
    }

    public static void Delete(BackupItem item)
    {
        try { File.Delete(item.FullPath); } catch { }
    }
}
