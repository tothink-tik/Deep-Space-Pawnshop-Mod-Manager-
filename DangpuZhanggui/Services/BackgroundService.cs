using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dangpu.Services;

public static class BackgroundService
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

    public static string Dir
    {
        get
        {
            var dir = Path.Combine(AppSettings.DataDir, "backgrounds");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static bool IsSupported(string path)
        => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static string? Import(string source)
    {
        try
        {
            if (!File.Exists(source) || !IsSupported(source)) return null;
            foreach (var old in Directory.GetFiles(Dir, "custom.*")) File.Delete(old);

            var dest = Path.Combine(Dir, "custom" + Path.GetExtension(source).ToLowerInvariant());
            File.Copy(source, dest, true);
            return dest;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? Load(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 1920;
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

    public static void ClearFiles()
    {
        try
        {
            foreach (var old in Directory.GetFiles(Dir, "custom.*")) File.Delete(old);
        }
        catch { }
    }
}
