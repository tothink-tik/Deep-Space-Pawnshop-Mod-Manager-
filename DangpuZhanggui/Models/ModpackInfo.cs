namespace Dangpu.Models;

public sealed class ModpackManifest
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Created { get; set; } = "";
    public ModpackGame Game { get; set; } = new();
    public List<ModpackMod> Mods { get; set; } = new();
    public bool IncludeConfig { get; set; }
}

public sealed class ModpackGame
{
    public string Name { get; set; } = "Probably Stolen";
    public string Version { get; set; } = "";
    public string BuildId { get; set; } = "";
    public string Loader { get; set; } = "";
    public string UnityVersion { get; set; } = "";
}

public sealed class ModpackMod
{
    public string Entry { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public string Source { get; set; } = "";
}

public sealed class ModpackInfo
{
    public string Path { get; init; } = "";
    public ModpackManifest Manifest { get; init; } = new();
    public long FileSize { get; init; }
    public DateTime Created { get; init; }
    public string? Error { get; init; }

    public string Name => string.IsNullOrWhiteSpace(Manifest.Name)
        ? System.IO.Path.GetFileNameWithoutExtension(Path)
        : Manifest.Name;
    public string Version => Manifest.Version;
    public string Author => string.IsNullOrWhiteSpace(Manifest.Author) ? "未知作者" : Manifest.Author;
    public string GameVersion => Manifest.Game.Version;
    public string BuildId => Manifest.Game.BuildId;
    public int ModCount => Manifest.Mods?.Count ?? 0;
    public string SizeText => FileSize < 1024 * 1024 ? $"{FileSize / 1024.0:0.#} KB" : $"{FileSize / 1024.0 / 1024.0:0.##} MB";
    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm");
    public string Subtitle => string.IsNullOrWhiteSpace(Version) ? Author : $"v{Version} · {Author}";
    public string GameLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(GameVersion)) parts.Add(GameVersion);
            if (!string.IsNullOrWhiteSpace(BuildId)) parts.Add($"build {BuildId}");
            if (Manifest.IncludeConfig) parts.Add("含配置");
            return string.Join(" · ", parts);
        }
    }
    public string ModsText => ModCount == 0
        ? "没有模组"
        : string.Join("、", Manifest.Mods.Select(m => string.IsNullOrWhiteSpace(m.Version) ? m.Name : $"{m.Name} {m.Version}"));
    public bool IsValid => Error == null;
}

public sealed class ModpackInstallResult
{
    public string BackupPath { get; init; } = "";
    public int ModCount { get; init; }
    public bool ConfigApplied { get; init; }
    public List<string> Warnings { get; init; } = new();
}
