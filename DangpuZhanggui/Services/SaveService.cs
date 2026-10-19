using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Dangpu.Services;

public sealed class SaveSnapshot
{
    public string FullPath { get; init; } = "";
    public DateTime Created { get; init; }
    public long Size { get; init; }
    public string Label { get; set; } = "";
    public List<string> Files { get; init; } = new();

    public string Name => Path.GetFileNameWithoutExtension(FullPath);
    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm");
    public string SizeText => Size < 1024 * 1024 ? $"{Size / 1024.0:0.#} KB" : $"{Size / 1024.0 / 1024.0:0.##} MB";
    public string FileListText => Files.Count == 0 ? "" : string.Join("、", Files);
}

public static class SaveService
{
    internal static string? RootOverride;

    public static string SaveDir => RootOverride ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "Questing Goose Studio", "Probably Stolen");

    private static string SnapshotDir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "backups", "saves");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static bool Exists => Directory.Exists(SaveDir);

    public sealed record SaveInfo(string FileName, DateTime Modified, long Size, int Count, long TotalSize);

    public static SaveInfo? CurrentInfo()
    {
        try
        {
            if (!Directory.Exists(SaveDir)) return null;
            var saves = Directory.GetFiles(SaveDir, "save_*.es3");
            if (saves.Length == 0) return null;
            var newest = new FileInfo(saves.OrderByDescending(File.GetLastWriteTimeUtc).First());
            var total = saves.Sum(f => new FileInfo(f).Length);
            return new SaveInfo(newest.Name, newest.LastWriteTime, newest.Length, saves.Length, total);
        }
        catch
        {
            return null;
        }
    }

    public static SaveSnapshot Create(string label)
    {
        if (!Directory.Exists(SaveDir)) throw new InvalidOperationException("没有找到存档目录：" + SaveDir);

        var files = new List<string>();
        foreach (var name in new[] { "SaveFile.es3", "saves_index.es3" })
        {
            var path = Path.Combine(SaveDir, name);
            if (File.Exists(path)) files.Add(name);
        }

        var newestSave = Directory.GetFiles(SaveDir, "save_*.es3")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (newestSave != null) files.Add(Path.GetFileName(newestSave));

        if (files.Count == 0) throw new InvalidOperationException("存档目录里没有可备份的文件");

        var safeLabel = string.Concat(label.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        var zipPath = Path.Combine(SnapshotDir, $"save-{DateTime.Now:yyyyMMdd-HHmmss}-{safeLabel}.zip");

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var file in files)
                zip.CreateEntryFromFile(Path.Combine(SaveDir, file), file, CompressionLevel.Optimal);

            var meta = zip.CreateEntry("dangpu-save.json");
            using var writer = new StreamWriter(meta.Open());
            writer.Write(JsonSerializer.Serialize(new
            {
                created = DateTime.Now,
                label,
                files
            }));
        }

        Trim();

        var fi = new FileInfo(zipPath);
        return new SaveSnapshot
        {
            FullPath = zipPath,
            Created = fi.LastWriteTime,
            Size = fi.Length,
            Label = label,
            Files = files
        };
    }

    public static List<SaveSnapshot> List()
    {
        var list = new List<SaveSnapshot>();
        try
        {
            foreach (var file in Directory.GetFiles(SnapshotDir, "save-*.zip"))
                list.Add(ReadSnapshot(file));
        }
        catch { }
        return list.OrderByDescending(s => s.Created).ToList();
    }

    public static void Restore(SaveSnapshot snapshot)
    {
        if (!File.Exists(snapshot.FullPath)) throw new FileNotFoundException("快照不存在", snapshot.FullPath);
        Directory.CreateDirectory(SaveDir);

        using var zip = ZipFile.OpenRead(snapshot.FullPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.Name.Length == 0) continue;
            if (entry.FullName.Equals("dangpu-save.json", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.FullName.Contains('/') || entry.FullName.Contains('\\') || entry.FullName.Contains(".."))
                throw new InvalidDataException("快照里有越界路径：" + entry.FullName);

            entry.ExtractToFile(Path.Combine(SaveDir, entry.Name), true);
        }
    }

    public static void Delete(SaveSnapshot snapshot)
    {
        try { File.Delete(snapshot.FullPath); } catch { }
    }

    private static SaveSnapshot ReadSnapshot(string path)
    {
        var fi = new FileInfo(path);
        var snapshot = new SaveSnapshot
        {
            FullPath = path,
            Created = fi.LastWriteTime,
            Size = fi.Length,
            Label = LabelFromName(fi.Name)
        };

        try
        {
            using var zip = ZipFile.OpenRead(path);
            var meta = zip.GetEntry("dangpu-save.json");
            if (meta != null)
            {
                using var reader = new StreamReader(meta.Open());
                using var doc = JsonDocument.Parse(reader.ReadToEnd());
                var root = doc.RootElement;
                if (root.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String)
                    snapshot.Label = label.GetString() ?? snapshot.Label;
                if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                    snapshot.Files.AddRange(files.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString() ?? "")
                        .Where(x => x.Length > 0));
            }
        }
        catch { }

        return snapshot;
    }

    private static string LabelFromName(string fileName)
    {
        // save-20261018-213000-手动.zip
        var name = Path.GetFileNameWithoutExtension(fileName);
        var parts = name.Split('-');
        return parts.Length >= 4 ? string.Join("-", parts.Skip(3)) : "";
    }

    private static void Trim(int keep = 20)
    {
        try
        {
            var files = Directory.GetFiles(SnapshotDir, "save-*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(keep)
                .ToList();

            foreach (var file in files) file.Delete();
        }
        catch { }
    }
}
