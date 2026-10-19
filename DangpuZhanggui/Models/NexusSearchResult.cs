using System.Windows.Media;

namespace Dangpu.Models;

public sealed class NexusSearchResult : ObservableObject
{
    public int ModId { get; init; }
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Version { get; init; } = "";
    public string PictureUrl { get; init; } = "";
    public string ThumbnailUrl { get; init; } = "";
    public int Downloads { get; init; }
    public int Endorsements { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool AdultContent { get; init; }

    public string PageUrl => $"https://www.nexusmods.com/probablystolen/mods/{ModId}";
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1].ToUpperInvariant();
    public string DownloadsText => Downloads >= 10000 ? $"{Downloads / 10000.0:0.#} 万" : Downloads.ToString("N0");
    public string MetaText => $"v{Version} · {DownloadsText} 下载" + (Endorsements > 0 ? $" · {Endorsements} 点赞" : "");
    public string UpdatedText => UpdatedAt is { } date ? date.ToLocalTime().ToString("yyyy-MM-dd") : "";

    public Brush TileBrush
    {
        get
        {
            var palette = new[]
            {
                (0xB5, 0x71, 0x3F), (0xA4, 0x5B, 0x4E), (0x5F, 0x8F, 0x8A), (0x8A, 0x7C, 0x4F),
                (0x9C, 0x6B, 0x4A), (0x6F, 0x7C, 0x5A), (0x8C, 0x5E, 0x69), (0x4F, 0x74, 0x72)
            };
            var (r, g, b) = palette[Math.Abs(ModId) % palette.Length];
            var brush = new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
            brush.Freeze();
            return brush;
        }
    }

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
}
