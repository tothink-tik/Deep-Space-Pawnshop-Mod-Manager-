using System.IO;
using System.Text.Json;

namespace Dangpu.Services;

/// <summary>玩家手动给模组绑定的 N 网页面，按 ModItem.Key 存。</summary>
public static class NexusLinkStore
{
    private static string FilePath => Path.Combine(AppSettings.DataDir, "nexus-links.json");

    public static Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath));
            return map == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Set(string key, string? url)
    {
        var map = Load();
        if (string.IsNullOrWhiteSpace(url)) map.Remove(key);
        else map[key] = url.Trim();

        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
