using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dangpu;

/// <summary>
/// --shot 模式：把每个页面渲染成 PNG，用来检查界面。正常使用时不会走到这里。
/// </summary>
public static class Shooter
{
    public static async void Run(MainWindow window, MainViewModel vm, string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            await Task.Delay(1000);

            for (int page = 0; page <= 9; page++)
            {
                vm.PageIndex = page;

                if (page == 1)
                {
                    var target = vm.Mods.FirstOrDefault(m => !m.HasLink) ?? vm.Mods.FirstOrDefault();
                    if (target != null) vm.SelectedMod = target;
                    window.UpdateLayout();
                    await Task.Delay(500);
                }

                window.UpdateLayout();
                await Task.Delay(650);
                Capture(window, Path.Combine(dir, $"page{page}.png"));

                if (page == 1)
                {
                    window.ShowDropOverlayForTest(new[]
                    {
                        @"C://Users//sir//Downloads//FutureTech.zip",
                        @"C://Users//sir//Downloads//Ratlin",
                        @"C://Users//sir//Downloads//RealAILearningModule.dll"
                    });
                    window.UpdateLayout();
                    await Task.Delay(400);
                    Capture(window, Path.Combine(dir, "page1-drop.png"));
                    window.HideDropOverlayForTest();

                    var linked = vm.Mods.FirstOrDefault(m => m.HasLink);
                    if (linked != null)
                    {
                        vm.SelectedMod = linked;
                        window.UpdateLayout();
                        await Task.Delay(1500);
                        Capture(window, Path.Combine(dir, "page1-cover.png"));
                    }
                }

                if (page == 8)
                {
                    vm.SearchQuery = "trade";
                    await vm.SearchNexusAsync(false);
                    await Task.Delay(1800);
                    Capture(window, Path.Combine(dir, "page8-search.png"));
                }

                if (page == 9 && (window.Host.Content as System.Windows.Controls.UserControl)?.Content
                        is System.Windows.Controls.ScrollViewer scroller)
                {
                    scroller.ScrollToEnd();
                    window.UpdateLayout();
                    await Task.Delay(350);
                    Capture(window, Path.Combine(dir, "page9-bottom.png"));
                }
            }
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

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static void Capture(Window window, string path)
    {
        if (window.Content is not FrameworkElement root) return;
        root.UpdateLayout();
        var w = (int)Math.Round(root.ActualWidth);
        var h = (int)Math.Round(root.ActualHeight);
        if (w <= 0 || h <= 0) return;

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
