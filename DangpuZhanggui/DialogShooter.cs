using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dangpu.Views;

namespace Dangpu;

/// <summary>--dlgshot：把三种确认框渲染成 PNG，检查配色和间距。正常使用不会走到。</summary>
public static class DialogShooter
{
    public static async void Run(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            await Task.Delay(400);

            await Capture(new MessageDialog("删除模组",
                "把「深空当铺：未来科技」移入回收站？\r\n\r\nC:////Program Files (x86)\\Steam\\steamapps\\common\\Probably Stolen Demo\\Mods\\FutureTech.dll",
                "移入回收站", "!", "取消", null, DialogTone.Danger), Path.Combine(dir, "dialog-danger.png"));

            await Capture(new MessageDialog("安装提醒",
                "FutureTech.dll 没有 MelonInfo，装进去也不会被加载。\r\nBrokenMod.dll 的程序集信息读不出来。",
                "知道了", "i", null, null, DialogTone.Warning), Path.Combine(dir, "dialog-info.png"));

            await Capture(new MessageDialog("安装 MelonLoader",
                "本地已经缓存了安装包（19.2 MB）。\r\n\r\n「用缓存的」不动网络；「重新下载」会拉最新版。",
                "用缓存的", "?", "重新下载", "取消", DialogTone.Info), Path.Combine(dir, "dialog-ask.png"));
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString()); } catch { }
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private static async Task Capture(MessageDialog dialog, string path)
    {
        dialog.Show();
        await Task.Delay(300);
        dialog.UpdateLayout();

        if (dialog.Content is FrameworkElement root)
        {
            var w = (int)Math.Round(root.ActualWidth);
            var h = (int)Math.Round(root.ActualHeight);
            if (w > 0 && h > 0)
            {
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(path);
                encoder.Save(fs);
            }
        }

        dialog.Close();
    }
}
