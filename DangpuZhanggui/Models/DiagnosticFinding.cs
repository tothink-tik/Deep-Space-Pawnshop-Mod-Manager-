using System.Windows.Media;

namespace Dangpu.Models;

public enum FindingLevel { Critical, Warning, Info }

public sealed class DiagnosticFinding
{
    public FindingLevel Level { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Suggestion { get; init; } = "";
    public string Evidence { get; init; } = "";
    public List<string> Suspects { get; init; } = new();
    public int Count { get; init; } = 1;

    public bool HasEvidence => !string.IsNullOrWhiteSpace(Evidence);
    public bool HasSuspects => Suspects.Count > 0;
    public string CountText => Count > 1 ? $"×{Count}" : "";
    public string LevelText => Level switch
    {
        FindingLevel.Critical => "错误",
        FindingLevel.Warning => "警告",
        _ => "提示"
    };

    public Brush Dot => Level switch
    {
        FindingLevel.Critical => Frozen(0xD0, 0x6A, 0x5D),
        FindingLevel.Warning => Frozen(0xD9, 0xA4, 0x5C),
        _ => Frozen(0x6E, 0x9A, 0x9C)
    };

    public Brush Fg => Level switch
    {
        FindingLevel.Critical => Frozen(0xEF, 0xA0, 0x93),
        FindingLevel.Warning => Frozen(0xE4, 0xB8, 0x78),
        _ => Frozen(0x9F, 0xC7, 0xC4)
    };

    public Brush Bg => Level switch
    {
        FindingLevel.Critical => Frozen(0x2F, 0x1E, 0x1C),
        FindingLevel.Warning => Frozen(0x2D, 0x25, 0x19),
        _ => Frozen(0x1F, 0x2A, 0x2B)
    };

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
        b2.Freeze();
        return b2;
    }
}
