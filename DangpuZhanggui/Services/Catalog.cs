using System.IO;
using System.Text.Json;

namespace Dangpu.Services;

public sealed class CatalogEntry
{
    public string BuiltInName { get; set; } = "";
    public string NameZh { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string SummaryZh { get; set; } = "";
    public string SummaryEn { get; set; } = "";
    public string Link { get; set; } = "";
}

/// <summary>
/// 读取游戏目录里的模组中文资料表（UserData/ModManager/mod-details.json）。
/// 没装那个 mod 时也能用：只是少了中文名和简介。
/// </summary>
public sealed class Catalog
{
    private readonly Dictionary<string, CatalogEntry> _map = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _map.Count;

    public static Catalog Load(GameInfo game)
    {
        var catalog = new Catalog();
        if (string.IsNullOrWhiteSpace(game.UserDataDir)) return catalog;

        var path = Path.Combine(game.UserDataDir, "ModManager", "mod-details.json");
        if (!File.Exists(path)) return catalog;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return catalog;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var e = new CatalogEntry
                {
                    BuiltInName = Get(prop.Value, "BuiltInName"),
                    NameZh = Get(prop.Value, "NameZh"),
                    NameEn = Get(prop.Value, "NameEn"),
                    SummaryZh = Get(prop.Value, "SummaryZh"),
                    SummaryEn = Get(prop.Value, "SummaryEn"),
                    Link = Get(prop.Value, "Link")
                };

                catalog.Add(prop.Name, e);
                catalog.Add(e.BuiltInName, e);
                catalog.Add(e.NameEn, e);
                catalog.Add(e.NameZh, e);
            }
        }
        catch { }

        return catalog;
    }

    private void Add(string? key, CatalogEntry e)
    {
        var n = Normalize(key);
        if (n.Length < 2) return;
        _map[n] = e;
    }

    private static string Get(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return "";
        foreach (var p in el.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
        return "";
    }

    public CatalogEntry? Find(string? melonName, string? assemblyName, string? fileBase)
    {
        foreach (var candidate in new[] { melonName, assemblyName, fileBase })
        {
            var n = Normalize(candidate);
            if (n.Length < 2) continue;
            if (_map.TryGetValue(n, out var hit)) return hit;

            var trimmed = n.EndsWith("mod", StringComparison.Ordinal) ? n[..^3] : n;
            if (trimmed.Length >= 2 && _map.TryGetValue(trimmed, out hit)) return hit;

            var cn = n.EndsWith("cn", StringComparison.Ordinal) ? n[..^2] : n;
            if (cn.Length >= 2 && _map.TryGetValue(cn, out hit)) return hit;
        }
        return null;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var chars = s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant);
        return new string(chars.ToArray());
    }
}
