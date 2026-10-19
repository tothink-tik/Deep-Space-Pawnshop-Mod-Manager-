using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dangpu.Models;

namespace Dangpu.Services;

public sealed class ProfileEntry
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Assembly { get; set; }
    public bool Enabled { get; set; }
}

public sealed class ModProfile
{
    public string Name { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
    public string GameVersion { get; set; } = "";
    public List<ProfileEntry> Mods { get; set; } = new();

    [JsonIgnore]
    public string FilePath { get; set; } = "";

    public int Total => Mods.Count;
    public int EnabledCount => Mods.Count(m => m.Enabled);
    public string CreatedText => Created.ToString("yyyy-MM-dd HH:mm");
    public string SummaryText => $"{Total} 个模组 · {EnabledCount} 启用";
}

public sealed class ProfileItem : ObservableObject
{
    public ModProfile Data { get; init; } = new();
    public string Title => Data.Name;
    public string CreatedText => Data.CreatedText;
    public string SummaryText => Data.SummaryText;
    public string GameVersionText => string.IsNullOrWhiteSpace(Data.GameVersion) ? "" : Data.GameVersion;

    private string _diffText = "";
    public string DiffText
    {
        get => _diffText;
        set => Set(ref _diffText, value);
    }

    private bool _hasDiff;
    public bool HasDiff
    {
        get => _hasDiff;
        set => Set(ref _hasDiff, value);
    }
}

public static class ProfileService
{
    public static string ProfileDir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "profiles");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static ModProfile CreateSnapshot(IEnumerable<ModItem> mods, string name, string gameVersion)
    {
        var profile = new ModProfile
        {
            Name = string.IsNullOrWhiteSpace(name) ? "未命名方案" : name.Trim(),
            Created = DateTime.Now,
            GameVersion = gameVersion
        };

        foreach (var mod in mods)
        {
            profile.Mods.Add(new ProfileEntry
            {
                Key = mod.Key,
                Name = mod.DisplayName,
                Assembly = string.IsNullOrWhiteSpace(mod.AssemblyName) ? null : mod.AssemblyName,
                Enabled = mod.Enabled
            });
        }

        return profile;
    }

    public static ModProfile Save(ModProfile profile)
    {
        var file = Path.Combine(ProfileDir, $"profile-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
        profile.FilePath = file;
        File.WriteAllText(file, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        return profile;
    }

    public static void Overwrite(ModProfile profile, string name)
    {
        profile.Name = name.Trim();
        if (string.IsNullOrWhiteSpace(profile.FilePath) || !File.Exists(profile.FilePath)) return;
        File.WriteAllText(profile.FilePath, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static List<ModProfile> List()
    {
        var list = new List<ModProfile>();
        try
        {
            foreach (var file in Directory.GetFiles(ProfileDir, "*.json"))
            {
                try
                {
                    var profile = JsonSerializer.Deserialize<ModProfile>(File.ReadAllText(file));
                    if (profile == null) continue;
                    profile.FilePath = file;
                    if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = Path.GetFileNameWithoutExtension(file);
                    list.Add(profile);
                }
                catch { }
            }
        }
        catch { }
        return list.OrderByDescending(p => p.Created).ToList();
    }

    public static void Delete(ModProfile profile)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(profile.FilePath) && File.Exists(profile.FilePath))
                File.Delete(profile.FilePath);
        }
        catch { }
    }

    public sealed record ApplyResult(int Changed, int Missing, int StrictDisabled);

    public static ApplyResult Apply(ModProfile profile, IReadOnlyList<ModItem> current, bool strict)
    {
        var matched = new HashSet<ModItem>();
        int changed = 0, missing = 0, strictDisabled = 0;

        foreach (var entry in profile.Mods)
        {
            var mod = current.FirstOrDefault(m => m.Key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
            if (mod == null)
            {
                missing++;
                continue;
            }

            matched.Add(mod);
            if (mod.Enabled == entry.Enabled) continue;

            var (ok, _) = ModScanner.SetEnabled(mod, entry.Enabled);
            if (ok) changed++;
        }

        if (strict)
        {
            foreach (var mod in current.Where(m => m.Enabled && !matched.Contains(m)))
            {
                var (ok, _) = ModScanner.SetEnabled(mod, false);
                if (ok) strictDisabled++;
            }
        }

        return new ApplyResult(changed, missing, strictDisabled);
    }

    public static (int diff, int missing) Compare(ModProfile profile, IReadOnlyList<ModItem> current)
    {
        int diff = 0, missing = 0;
        var byKey = current.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in profile.Mods)
        {
            if (!byKey.TryGetValue(entry.Key, out var mod))
            {
                missing++;
                continue;
            }
            if (mod.Enabled != entry.Enabled) diff++;
        }

        return (diff, missing);
    }
}
