using System.IO;
using System.Windows.Media;

namespace Dangpu.Models;

public enum IssueLevel { Info, Warning, Error }

public sealed class ModIssue
{
    public IssueLevel Level { get; init; }
    public string Text { get; init; } = "";
    public Brush Dot => Level switch
    {
        IssueLevel.Error => Brush(0xD0, 0x6A, 0x5D),
        IssueLevel.Warning => Brush(0xD9, 0xA4, 0x5C),
        _ => Brush(0x8C, 0x86, 0x7C)
    };
    public Brush Fg => Level switch
    {
        IssueLevel.Error => Brush(0xEF, 0xA0, 0x93),
        IssueLevel.Warning => Brush(0xE4, 0xB8, 0x78),
        _ => Brush(0xBC, 0xB6, 0xAA)
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}

public sealed class Chip
{
    public string Text { get; init; } = "";
    public Brush Fg { get; init; } = Brushes.Gray;
    public Brush Bg { get; init; } = Brushes.Transparent;
    public string Tip { get; init; } = "";

    public static SolidColorBrush C(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public static Chip Success(string t, string tip = "") => new() { Text = t, Fg = C(0x8F, 0xD3, 0xB0), Bg = C(0x1D, 0x2E, 0x26), Tip = tip };
    public static Chip Warn(string t, string tip = "") => new() { Text = t, Fg = C(0xE4, 0xB8, 0x78), Bg = C(0x2E, 0x26, 0x19), Tip = tip };
    public static Chip Danger(string t, string tip = "") => new() { Text = t, Fg = C(0xEF, 0xA0, 0x93), Bg = C(0x30, 0x1F, 0x1D), Tip = tip };
    public static Chip Neutral(string t, string tip = "") => new() { Text = t, Fg = C(0xB9, 0xB3, 0xA7), Bg = C(0x26, 0x2E, 0x31), Tip = tip };
    public static Chip Accent(string t, string tip = "") => new() { Text = t, Fg = C(0xE8, 0xB2, 0x7B), Bg = C(0x33, 0x26, 0x1A), Tip = tip };
}

public sealed class ModItem : ObservableObject
{
    public string Path { get; set; } = "";
    public bool IsFolder { get; set; }
    public string FileName { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }

    public string AssemblyName { get; set; } = "";
    public string AssemblyVersion { get; set; } = "";
    public string? MelonName { get; set; }
    public string? MelonVersion { get; set; }
    public string? Author { get; set; }
    public string? DownloadLink { get; set; }
    public string? GameName { get; set; }
    public string? GameDeveloper { get; set; }
    public bool HasMelonInfo { get; set; }
    public bool HasMelonGame { get; set; }
    public bool HasManifest { get; set; }

    public int DllCount { get; set; }
    public List<string> DllNames { get; } = new();
    public List<string> References { get; } = new();
    public List<string> Dependencies { get; } = new();
    public List<ModIssue> Issues { get; } = new();

    public string? CatalogZh { get; set; }
    public string? CatalogEn { get; set; }
    public string? CatalogSummary { get; set; }
    public string? CatalogLink { get; set; }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!Set(ref _enabled, value)) return;
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(StateChip));
            OnPropertyChanged(nameof(ToggleActionText));
            OnPropertyChanged(nameof(Chips));
        }
    }

    private string _logState = "";
    public string LogState
    {
        get => _logState;
        set
        {
            if (!Set(ref _logState, value)) return;
            OnPropertyChanged(nameof(LogChip));
            OnPropertyChanged(nameof(Chips));
        }
    }

    /// <summary>方案里用的稳定键：文件名去掉 .dll/.disabled，子文件夹加前缀。</summary>
    public string Key => (IsFolder ? "folder:" : "file:") + (IsFolder ? FileName : FileBaseName);

    public string FileBaseName
    {
        get
        {
            var n = FileName;
            if (n.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) n = n[..^9];
            if (n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
            return n;
        }
    }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(CatalogZh) ? CatalogZh! :
        !string.IsNullOrWhiteSpace(MelonName) ? MelonName! :
        !string.IsNullOrWhiteSpace(AssemblyName) ? AssemblyName :
        FileBaseName;

    public string EnglishName =>
        !string.IsNullOrWhiteSpace(CatalogEn) ? CatalogEn! :
        !string.IsNullOrWhiteSpace(MelonName) ? MelonName! : AssemblyName;

    public string SecondaryName =>
        string.Equals(EnglishName, DisplayName, StringComparison.OrdinalIgnoreCase) ? "" : EnglishName;

    public bool HasSecondaryName => !string.IsNullOrWhiteSpace(SecondaryName);

    public bool HasIssues => Issues.Count > 0;

    /// <summary>手动绑定的 N 网页面，优先于资料表里的链接。</summary>
    private string? _manualLink;
    public string? ManualLink
    {
        get => _manualLink;
        set
        {
            if (!Set(ref _manualLink, value)) return;
            OnPropertyChanged(nameof(NexusLink));
            OnPropertyChanged(nameof(HasLink));
            OnPropertyChanged(nameof(HasManualLink));
        }
    }

    public string? NexusLink =>
        !string.IsNullOrWhiteSpace(ManualLink) ? ManualLink :
        !string.IsNullOrWhiteSpace(CatalogLink) ? CatalogLink : null;

    public bool HasLink => !string.IsNullOrWhiteSpace(NexusLink);

    public bool HasManualLink => !string.IsNullOrWhiteSpace(ManualLink);

    private string _nexusVersion = "";
    public string NexusVersion
    {
        get => _nexusVersion;
        set
        {
            if (!Set(ref _nexusVersion, value)) return;
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(Chips));
        }
    }

    private bool _hasUpdate;
    public bool HasUpdate
    {
        get => _hasUpdate;
        set
        {
            if (!Set(ref _hasUpdate, value)) return;
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(Chips));
        }
    }

    public string UpdateText => HasUpdate && !string.IsNullOrWhiteSpace(NexusVersion)
        ? $"N 网 {NexusVersion}"
        : "";

    // ---------------- Nexus 信息 ----------------

    private string _nexusAuthor = "";
    public string NexusAuthor
    {
        get => _nexusAuthor;
        set
        {
            if (!Set(ref _nexusAuthor, value)) return;
            OnPropertyChanged(nameof(AuthorText));
        }
    }

    private string _nexusDescription = "";
    public string NexusDescription
    {
        get => _nexusDescription;
        set
        {
            if (!Set(ref _nexusDescription, value)) return;
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(HasDescription));
            OnPropertyChanged(nameof(HasLongDescription));
        }
    }

    private bool _descriptionExpanded;
    public bool DescriptionExpanded
    {
        get => _descriptionExpanded;
        set
        {
            if (!Set(ref _descriptionExpanded, value)) return;
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(DescriptionToggleText));
        }
    }

    public string AuthorText =>
        !string.IsNullOrWhiteSpace(NexusAuthor) ? NexusAuthor :
        !string.IsNullOrWhiteSpace(Author) ? Author! : "未知作者";

    public string DescriptionText
    {
        get
        {
            var text = !string.IsNullOrWhiteSpace(NexusDescription) ? NexusDescription : (CatalogSummary ?? "");
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (!DescriptionExpanded && text.Length > 1200)
                return text[..1200].TrimEnd() + "…";
            return text;
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);
    public bool HasLongDescription => (NexusDescription?.Length ?? 0) > 1200;
    public string DescriptionToggleText => DescriptionExpanded ? "收起" : "展开全文";

    private ImageSource? _cover;
    public ImageSource? Cover
    {
        get => _cover;
        set
        {
            if (!Set(ref _cover, value)) return;
            OnPropertyChanged(nameof(HasCover));
        }
    }

    public bool HasCover => Cover != null;

    private bool _coverLoading;
    public bool CoverLoading
    {
        get => _coverLoading;
        set => Set(ref _coverLoading, value);
    }

    private string _nexusState = "";
    public string NexusState
    {
        get => _nexusState;
        set
        {
            if (!Set(ref _nexusState, value)) return;
            OnPropertyChanged(nameof(NexusHint));
            OnPropertyChanged(nameof(HasNexusHint));
            OnPropertyChanged(nameof(NeedsNexusKey));
        }
    }

    public bool NexusLoaded { get; set; }

    public string NexusHint => NexusState switch
    {
        "no-key" => "填一个 Nexus API key，就能自动读取 N 网封面、作者和完整简介。",
        "bad-key" => "Nexus API key 无效或过期了，去设置里检查一下。",
        "rate" => "Nexus 接口请求太频繁，过一会儿再试。",
        "error" => "N 网信息没读出来，可能是网络问题。",
        "loading" => "正在读取 N 网信息…",
        _ => ""
    };

    public bool HasNexusHint => NexusHint.Length > 0;
    public bool NeedsNexusKey => NexusState is "no-key" or "bad-key";

    public string SubLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.Equals(EnglishName, DisplayName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(EnglishName))
                parts.Add(EnglishName);
            if (!string.IsNullOrWhiteSpace(Author)) parts.Add(Author!);
            if (parts.Count == 0) parts.Add(FileName);
            return string.Join("  ·  ", parts);
        }
    }

    public string VersionText
    {
        get
        {
            var v = !string.IsNullOrWhiteSpace(MelonVersion) ? MelonVersion! : AssemblyVersion;
            if (string.IsNullOrWhiteSpace(v)) return "";
            var t = v.Trim();
            var segments = t.Split('.').ToList();
            while (segments.Count > 2 && segments[^1] == "0") segments.RemoveAt(segments.Count - 1);
            t = string.Join(".", segments);
            return "v" + t;
        }
    }

    public bool HasVersion => !string.IsNullOrWhiteSpace(VersionText);

    public string ToggleActionText => Enabled ? "停用这个模组" : "启用这个模组";

    public string SizeText => Size switch
    {
        < 1024 => $"{Size} B",
        < 1024 * 1024 => $"{Size / 1024.0:0.#} KB",
        _ => $"{Size / 1024.0 / 1024.0:0.##} MB"
    };

    public string ModifiedText => Modified.ToString("yyyy-MM-dd HH:mm");

    public string StateText => Enabled ? "已启用" : "已停用";

    public Chip StateChip => Enabled ? Chip.Success("已启用") : Chip.Neutral("已停用");

    public Chip? LogChip => LogState switch
    {
        "loaded" => Chip.Success("日志已加载", "Latest.log 中有加载记录"),
        "failed" => Chip.Danger("加载失败", "Latest.log 中有加载失败记录"),
        "absent" => Chip.Neutral("日志未见", "Latest.log 中没有它的加载记录，可能刚安装或游戏未启动"),
        _ => null
    };

    public string Initial
    {
        get
        {
            var s = DisplayName;
            return string.IsNullOrWhiteSpace(s) ? "?" : s.Substring(0, 1).ToUpperInvariant();
        }
    }

    public Brush TileBrush { get; set; } = Brushes.SteelBlue;

    public bool HasError => Issues.Any(i => i.Level == IssueLevel.Error);
    public bool HasWarning => Issues.Any(i => i.Level == IssueLevel.Warning);

    public IEnumerable<Chip> Chips
    {
        get
        {
            if (HasError) yield return Chip.Danger("有问题");
            else if (HasWarning) yield return Chip.Warn("注意");
            if (HasUpdate) yield return Chip.Accent("可更新 " + NexusVersion, "N 网有更新的版本");
            var log = LogChip;
            if (log != null) yield return log;
        }
    }

    public string SearchText => string.Join(" ", new[]
    {
        DisplayName, EnglishName, AssemblyName, FileName, Author ?? "", CatalogSummary ?? "", MelonName ?? ""
    }).ToLowerInvariant();

    public static Brush TileFor(string seed)
    {
        var palette = new[]
        {
            (0xB5, 0x71, 0x3F), (0xA4, 0x5B, 0x4E), (0x5F, 0x8F, 0x8A), (0x8A, 0x7C, 0x4F),
            (0x9C, 0x6B, 0x4A), (0x6F, 0x7C, 0x5A), (0x8C, 0x5E, 0x69), (0x4F, 0x74, 0x72)
        };
        var h = 17;
        foreach (var ch in seed) h = unchecked(h * 31 + ch);
        var c = palette[Math.Abs(h) % palette.Length];
        var b = new SolidColorBrush(Color.FromRgb((byte)c.Item1, (byte)c.Item2, (byte)c.Item3));
        b.Freeze();
        return b;
    }
}
