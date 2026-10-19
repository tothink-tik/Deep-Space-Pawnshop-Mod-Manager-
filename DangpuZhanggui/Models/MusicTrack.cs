using System.IO;

namespace Dangpu.Models;

public sealed class MusicTrack
{
    public string Path { get; init; } = "";
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public string FileName => System.IO.Path.GetFileName(Path);
}
