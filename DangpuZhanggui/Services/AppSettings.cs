using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Dangpu.Services;

public sealed class AppSettings
{
    public string? GamePath { get; set; }
    public bool HideConsole { get; set; } = true;
    public bool DebugMode { get; set; }
    public bool CapturePlayerLogs { get; set; }
    public bool NoMods { get; set; }
    public string ExtraArgs { get; set; } = "";
    public string NexusApiKey { get; set; } = "";
    public string Accent { get; set; } = "#E0A15A";
    public string UiStyle { get; set; } = "terminal";
    public int LastPage { get; set; }
    public bool AutoRefreshLog { get; set; } = true;
    public bool AutoSaveBeforeProfile { get; set; } = true;
    public string BackgroundPath { get; set; } = "";
    public double BackgroundDim { get; set; } = 0.78;
    public double BackgroundBlur { get; set; } = 18;
    public List<string> MusicFiles { get; set; } = new();
    public double MusicVolume { get; set; } = 0.55;
    public bool MusicLoop { get; set; } = true;
    public bool MusicAutoPlay { get; set; }

    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DangpuZhanggui");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string FilePath => Path.Combine(DataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s != null) return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public static class ThemeService
{
    public static readonly (string Name, string Hex)[] Presets =
    {
        ("琥珀", "#E0A15A"),
        ("铜绿", "#4E9E94"),
        ("锈红", "#C9635A"),
        ("冷蓝", "#5F86C0")
    };

    private sealed record Palette(
        string Bg, string Surface, string SurfaceAlt, string Hover, string Border, string BorderStrong,
        string Text, string TextSub, string TextMuted, string TextFaint,
        string SuccessSoft, string WarnSoft, string DangerSoft, string InfoSoft,
        string Scroll, string ScrollHover, string DefaultAccent);

    private static readonly Palette Terminal = new(
        "#0A1211", "#101B19", "#162522", "#1E2F2B", "#263732", "#3A4E48",
        "#EAE5D7", "#B9B4A7", "#8A877C", "#6B6960",
        "#13251F", "#2A2316", "#2A1917", "#142A27",
        "#3A4A45", "#4C5F58", "#E0A15A");

    private static readonly Palette Night = new(
        "#10171A", "#151F22", "#1B272A", "#223034", "#2A3A3D", "#3C5053",
        "#F1ECE2", "#B8B2A6", "#8A857B", "#6A665D",
        "#1D2E26", "#2E2619", "#301F1D", "#1E2629",
        "#3B4A4E", "#4C5E62", "#E0A15A");

    private static readonly Palette Graphite = new(
        "#15171B", "#1B1E23", "#21252B", "#282D34", "#2C3138", "#3A414A",
        "#ECEEF2", "#B6BAC2", "#858A94", "#656A74",
        "#1C2A24", "#2A2419", "#2A1B1A", "#1E2430",
        "#343A43", "#444C57", "#5F86C0");

    private static readonly Color DarkInk = Color.FromRgb(0x1A, 0x12, 0x07);

    public static void ApplyStyle(string style, string accentHex)
    {
        var palette = style switch
        {
            "night" => Night,
            "graphite" => Graphite,
            _ => Terminal
        };

        var app = Application.Current;
        if (app == null) return;

        SetBrush(app, "BgBrush", palette.Bg);
        SetBrush(app, "SurfaceBrush", palette.Surface);
        SetBrush(app, "SurfaceAltBrush", palette.SurfaceAlt);
        SetBrush(app, "HoverBrush", palette.Hover);
        SetBrush(app, "BorderBrush", palette.Border);
        SetBrush(app, "BorderStrongBrush", palette.BorderStrong);
        SetBrush(app, "TextBrush", palette.Text);
        SetBrush(app, "TextSubBrush", palette.TextSub);
        SetBrush(app, "TextMutedBrush", palette.TextMuted);
        SetBrush(app, "TextFaintBrush", palette.TextFaint);
        SetBrush(app, "SuccessSoftBrush", palette.SuccessSoft);
        SetBrush(app, "WarnSoftBrush", palette.WarnSoft);
        SetBrush(app, "DangerSoftBrush", palette.DangerSoft);
        SetBrush(app, "InfoSoftBrush", palette.InfoSoft);
        SetBrush(app, "ScrollThumbBrush", palette.Scroll);
        SetBrush(app, "ScrollThumbHoverBrush", palette.ScrollHover);

        if (ColorConverter.ConvertFromString(accentHex) is not Color accent)
            accent = (Color)ColorConverter.ConvertFromString(palette.DefaultAccent)!;

        var surface = (Color)ColorConverter.ConvertFromString(palette.Surface)!;
        app.Resources["AccentBrush"] = Brush(accent);
        app.Resources["AccentHoverBrush"] = Brush(Darken(accent, 0.10));
        app.Resources["AccentSoftBrush"] = Brush(Blend(accent, surface, 0.86));
        app.Resources["AccentBorderBrush"] = Brush(Blend(accent, surface, 0.55));
        app.Resources["AccentForegroundBrush"] = Brush(ReadableInk(accent));

        var metrics = ThemeMetrics.Current;
        app.Resources["CardRadius"] = metrics.CardRadius;
        app.Resources["ControlRadius"] = metrics.ControlRadius;
        app.Resources["RowRadius"] = metrics.RowRadius;
        app.Resources["NavRadius"] = metrics.NavRadius;
        metrics.CardRadius = style switch
        {
            "night" => new CornerRadius(10),
            "graphite" => new CornerRadius(8),
            _ => new CornerRadius(2)
        };
        metrics.ControlRadius = style switch
        {
            "night" => new CornerRadius(8),
            "graphite" => new CornerRadius(6),
            _ => new CornerRadius(2)
        };
        metrics.RowRadius = style switch
        {
            "night" => new CornerRadius(9),
            "graphite" => new CornerRadius(7),
            _ => new CornerRadius(2)
        };
        metrics.NavRadius = style switch
        {
            "night" => new CornerRadius(8),
            "graphite" => new CornerRadius(6),
            _ => new CornerRadius(0)
        };
        metrics.HudVisibility = style == "terminal" ? Visibility.Visible : Visibility.Collapsed;
        app.Resources["CardRadius"] = metrics.CardRadius;
        app.Resources["ControlRadius"] = metrics.ControlRadius;
        app.Resources["RowRadius"] = metrics.RowRadius;
        app.Resources["NavRadius"] = metrics.NavRadius;
    }

    /// <summary>把 Card 样式的圆角按当前风格直接写到 Border 上（Style 里的 DynamicResource 对结构体不可靠）。</summary>
    public static void ApplyRadii(DependencyObject? root)
    {
        if (root == null) return;
        var app = Application.Current;
        if (app?.Resources["Card"] is not Style cardStyle) return;
        var radius = ThemeMetrics.Current.CardRadius;

        foreach (var border in Descendants<System.Windows.Controls.Border>(root))
        {
            if (ReferenceEquals(border.Style, cardStyle))
                border.CornerRadius = radius;
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void SetBrush(Application app, string key, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color color)
            app.Resources[key] = Brush(color);
    }

    /// <summary>亮底配深字，暗底配白字，按对比度算，不猜。</summary>
    private static Color ReadableInk(Color background)
    {
        var onDark = Contrast(background, Colors.White);
        var onLight = Contrast(background, DarkInk);
        return onLight >= onDark ? DarkInk : Colors.White;
    }

    private static double Contrast(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var hi = Math.Max(la, lb);
        var lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static SolidColorBrush Brush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Color Darken(Color c, double amount) => Color.FromRgb(
        (byte)Math.Max(0, c.R * (1 - amount)),
        (byte)Math.Max(0, c.G * (1 - amount)),
        (byte)Math.Max(0, c.B * (1 - amount)));

    private static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
        (byte)(a.R * (1 - amount) + b.R * amount),
        (byte)(a.G * (1 - amount) + b.G * amount),
        (byte)(a.B * (1 - amount) + b.B * amount));
}
