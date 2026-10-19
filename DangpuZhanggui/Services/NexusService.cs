using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dangpu.Models;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dangpu.Services;

public sealed class NexusModInfo
{
    public int ModId { get; set; }
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string PictureUrl { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime FetchedAt { get; set; }
}

public enum NexusSearchSort { Relevance, Downloads, Endorsements, Updated }

public sealed record NexusSearchPage(int Total, List<NexusSearchResult> Results);

public sealed class NexusAuthException : Exception
{
    public NexusAuthException() : base("Nexus API key 无效或没有权限") { }
}

public sealed class NexusRateLimitException : Exception
{
    public NexusRateLimitException() : base("Nexus 接口请求太频繁") { }
}

public static class NexusService
{
    private static readonly Regex LinkRegex = new(
        @"nexusmods\.com/(?<game>[^/?#]+)/mods/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BbCodeRegex = new(@"\[/?[a-z*][^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex BlankLinesRegex = new(@"\n{3,}", RegexOptions.Compiled);

    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "cache", "nexus");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>N 网版本是否比本地新，只比较数字段。</summary>
    public static bool IsNewer(string? remote, string? local)
    {
        var r = Numbers(remote);
        var l = Numbers(local);
        if (r.Length == 0 || l.Length == 0) return false;

        for (int i = 0; i < Math.Max(r.Length, l.Length); i++)
        {
            var a = i < r.Length ? r[i] : 0;
            var b = i < l.Length ? l[i] : 0;
            if (a > b) return true;
            if (a < b) return false;
        }
        return false;
    }

    private static int[] Numbers(string? version) =>
        Regex.Matches(version ?? "", @"\d+")
            .Select(m => int.TryParse(m.Value, out var n) ? n : 0)
            .Take(4)
            .ToArray();

    public static (string game, int id)? ParseLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        var match = LinkRegex.Match(link);
        if (!match.Success) return null;
        if (!int.TryParse(match.Groups["id"].Value, out var id)) return null;
        return (match.Groups["game"].Value.ToLowerInvariant(), id);
    }

    /// <summary>搜索走官方 v2 GraphQL；v1 的 /mods/search 实际会被当成 mod id。</summary>
    public static async Task<NexusSearchPage> SearchAsync(string game, string query, NexusSearchSort sort,
        int offset, int count, string apiKey, CancellationToken ct)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var sortField = sort switch
        {
            NexusSearchSort.Downloads => "downloads",
            NexusSearchSort.Endorsements => "endorsements",
            NexusSearchSort.Updated => "updatedAt",
            _ => string.IsNullOrWhiteSpace(query) ? "downloads" : "relevance"
        };

        var filter = hasQuery
            ? "gameDomainName: [{value: $game, op: EQUALS}], name: [{value: $q, op: WILDCARD}]"
            : "gameDomainName: [{value: $game, op: EQUALS}]";
        var signature = hasQuery
            ? "query($game: String!, $q: String!, $offset: Int!, $count: Int!)"
            : "query($game: String!, $offset: Int!, $count: Int!)";

        var gql = $@"{signature} {{
  mods(filter: {{ {filter} }}, sort: [{{ {sortField}: {{ direction: DESC }} }}], offset: $offset, count: $count) {{
    totalCount
    nodes {{ modId name summary author version pictureUrl thumbnailUrl downloads endorsements updatedAt adultContent }}
  }}
}}";

        var variables = new Dictionary<string, object?>
        {
            ["game"] = game,
            ["offset"] = offset,
            ["count"] = count
        };
        if (hasQuery) variables["q"] = query.Trim();

        using var client = NewApiClient(apiKey);
        var payload = JsonSerializer.Serialize(new { query = gql, variables });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("https://api.nexusmods.com/v2/graphql", content, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new NexusAuthException();
        if ((int)response.StatusCode == 429) throw new NexusRateLimitException();

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Nexus 搜索失败（HTTP {(int)response.StatusCode}）");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("errors", out var errors) &&
            errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var message = errors[0].TryGetProperty("message", out var m) ? m.GetString() : null;
            throw new InvalidOperationException(message ?? "Nexus 搜索出错");
        }

        var mods = doc.RootElement.GetProperty("data").GetProperty("mods");
        var total = mods.GetProperty("totalCount").GetInt32();
        var results = new List<NexusSearchResult>();

        if (mods.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                DateTime? updated = null;
                if (node.TryGetProperty("updatedAt", out var updatedElement) &&
                    updatedElement.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(updatedElement.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var parsed))
                    updated = parsed;

                results.Add(new NexusSearchResult
                {
                    ModId = node.GetProperty("modId").GetInt32(),
                    Name = GetString(node, "name"),
                    Summary = GetString(node, "summary"),
                    Author = GetString(node, "author"),
                    Version = GetString(node, "version"),
                    PictureUrl = GetString(node, "pictureUrl"),
                    ThumbnailUrl = GetString(node, "thumbnailUrl"),
                    Downloads = node.TryGetProperty("downloads", out var d) ? d.GetInt32() : 0,
                    Endorsements = node.TryGetProperty("endorsements", out var e) ? e.GetInt32() : 0,
                    UpdatedAt = updated,
                    AdultContent = node.TryGetProperty("adultContent", out var a) && a.ValueKind == JsonValueKind.True
                });
            }
        }

        return new NexusSearchPage(total, results);
    }

    public static async Task<ImageSource?> GetSearchThumbnailAsync(NexusSearchResult result, CancellationToken ct)
    {
        var path = Path.Combine(CacheDir, $"{result.ModId}-thumb.img");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 512)
            {
                var url = string.IsNullOrWhiteSpace(result.ThumbnailUrl) ? result.PictureUrl : result.ThumbnailUrl;
                if (string.IsNullOrWhiteSpace(url)) return null;

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DangpuZhanggui/1.1.0");
                var bytes = await client.GetByteArrayAsync(url, ct);
                if (bytes.Length < 512) return null;
                await File.WriteAllBytesAsync(path, bytes, ct);
            }

            return LoadCover(path, 420);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient NewApiClient(string apiKey)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.Add("apikey", apiKey);
        client.DefaultRequestHeaders.Add("Application-Name", "DangpuZhanggui");
        client.DefaultRequestHeaders.Add("Application-Version", "1.1.0");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DangpuZhanggui/1.1.0");
        return client;
    }

    public static async Task<NexusModInfo?> GetModInfoAsync(string game, int modId, string apiKey,
        CancellationToken ct, bool force = false)
    {
        var cached = ReadCache(modId);
        if (!force && cached != null && DateTime.Now - cached.FetchedAt < TimeSpan.FromDays(7))
            return cached;

        try
        {
            using var client = NewApiClient(apiKey);

            var url = $"https://api.nexusmods.com/v1/games/{Uri.EscapeDataString(game)}/mods/{modId}.json";
            using var response = await client.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new NexusAuthException();
            if ((int)response.StatusCode == 429) throw new NexusRateLimitException();
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var info = new NexusModInfo
            {
                ModId = modId,
                Name = GetString(root, "name"),
                Author = GetString(root, "author"),
                Summary = GetString(root, "summary"),
                Description = GetString(root, "description"),
                PictureUrl = GetString(root, "picture_url"),
                Version = GetString(root, "version"),
                FetchedAt = DateTime.Now
            };

            WriteCache(info);
            return info;
        }
        catch (NexusAuthException)
        {
            throw;
        }
        catch (NexusRateLimitException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch when (cached != null)
        {
            return cached;
        }
    }

    public static async Task<string?> GetCoverAsync(NexusModInfo info, CancellationToken ct)
    {
        var path = Path.Combine(CacheDir, $"{info.ModId}.img");
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 1024) return path;
            if (string.IsNullOrWhiteSpace(info.PictureUrl)) return null;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DangpuZhanggui/3.1.0");

            var bytes = await client.GetByteArrayAsync(info.PictureUrl, ct);
            if (bytes.Length < 1024) return null;

            await File.WriteAllBytesAsync(path, bytes, ct);
            return path;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? LoadCover(string path, int decodeWidth = 720)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>N 网的 description 是 HTML/BBCode 混排，转成能直接显示和滚动的纯文本。</summary>
    public static string ToPlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var text = input;
        text = Regex.Replace(text, @"\[img\].*?\[/img\]", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"\[br\s*\]", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[list[^\]]*\]", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[/list\]", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[\*\]", "\n• ", RegexOptions.IgnoreCase);
        text = BbCodeRegex.Replace(text, "");
        text = Regex.Replace(text, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/?\s*(?:p|div)[^>]*>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*li[^>]*>", "\n• ", RegexOptions.IgnoreCase);
        text = HtmlTagRegex.Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = Regex.Replace(text, @"[ \t]+\n", "\n");
        text = Regex.Replace(text, @"\n[ \t]*\n(?=• )", "\n");
        text = BlankLinesRegex.Replace(text, "\n\n");
        text = text.Trim();

        return text.Length > 12_000 ? text[..12_000].TrimEnd() + "…" : text;
    }

    private static string GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return "";
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.Null => "",
            _ => el.ToString()
        };
    }

    private static NexusModInfo? ReadCache(int modId)
    {
        try
        {
            var path = Path.Combine(CacheDir, $"{modId}.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<NexusModInfo>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteCache(NexusModInfo info)
    {
        try
        {
            var path = Path.Combine(CacheDir, $"{info.ModId}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
