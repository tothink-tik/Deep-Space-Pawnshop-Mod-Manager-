using System.IO;
using System.Text;
using System.Windows;
using Dangpu.Services;

namespace Dangpu;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(AppSettings.DataDir, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {args.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }

            MessageBox.Show("出了点问题：" + args.Exception.Message + Environment.NewLine + Environment.NewLine +
                            $"详细信息写进了 {Path.Combine(AppSettings.DataDir, "error.log")}",
                "深空当铺mod局外管理", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var vm = new MainViewModel();
        ThemeService.ApplyStyle(vm.Settings.UiStyle, vm.Settings.Accent);

        if (TryHandleTestArgs(e.Args, vm)) return;

        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();

        if (e.Args.Length >= 2 && e.Args[0] == "--shot")
            Shooter.Run(window, vm, e.Args[1]);
    }

    private bool TryHandleTestArgs(string[] args, MainViewModel vm)
    {
        if (args.Length < 2) return false;

        switch (args[0])
        {
            case "--dump":
                File.WriteAllText(args[1], vm.DumpJson());
                Shutdown();
                return true;
            case "--selftest":
                File.WriteAllText(args[1], SelfTest.Run(vm.Game));
                Shutdown();
                return true;
            case "--loadertest":
                File.WriteAllText(args[1], LoaderSelfTest.Run(vm.Game));
                Shutdown();
                return true;
            case "--profiletest":
                File.WriteAllText(args[1], ProfileSelfTest.Run(vm.Game));
                Shutdown();
                return true;
            case "--importtest":
                File.WriteAllText(args[1], ImportSelfTest.Run(vm.Game));
                Shutdown();
                return true;
            case "--dlgshot":
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                DialogShooter.Run(args[1]);
                return true;
            case "--nexustest":
                RunNexusTest(vm, args[1]);
                return true;
            case "--packtest":
                RunPackTest(vm, args[1]);
                Shutdown();
                return true;
            default:
                return false;
        }
    }

    private static void RunPackTest(MainViewModel vm, string outPath)
    {
        var report = new StringBuilder();
        var enabled = vm.Mods.Where(m => m.Enabled).ToList();
        report.AppendLine($"enabled mods: {enabled.Count}");

        var info = ModpackService.Create(vm.Game, enabled, "示例整合包", "1.1", "totrymod", "1.1 功能演示", true, outPath);
        report.AppendLine($"created: {info.Name} v{info.Version}, mods={info.ModCount}, size={info.FileSize}");

        var read = ModpackService.Read(outPath);
        report.AppendLine($"read: {read.Name}, mods={read.ModCount}, config={read.Manifest.IncludeConfig}");

        var temp = Path.Combine(Path.GetTempPath(), "dangpu-packtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.WriteAllText(Path.Combine(temp, "Probably Stolen.exe"), "");
        Directory.CreateDirectory(Path.Combine(temp, "Mods"));
        Directory.CreateDirectory(Path.Combine(temp, "UserData"));

        var fake = new GameInfo
        {
            GameDir = temp,
            ExePath = Path.Combine(temp, "Probably Stolen.exe")
        };
        var result = ModpackService.Install(fake, outPath, checkRunning: false);
        report.AppendLine($"install: mods={result.ModCount}, config={result.ConfigApplied}, backup={result.BackupPath}");
        foreach (var file in Directory.GetFiles(Path.Combine(temp, "Mods"), "*", SearchOption.AllDirectories))
            report.AppendLine("  " + Path.GetRelativePath(temp, file));

        try { File.Delete(result.BackupPath); } catch { }
        try { Directory.Delete(temp, true); } catch { }

        File.WriteAllText(outPath + ".txt", report.ToString());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        MusicService.Shutdown();
        base.OnExit(e);
    }

    private async void RunNexusTest(MainViewModel vm, string path)
    {
        var result = await vm.NexusSelfTestAsync();
        try { File.WriteAllText(path, result); } catch { }
        Shutdown();
    }
}
