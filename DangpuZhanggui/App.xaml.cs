using System.IO;
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
            default:
                return false;
        }
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
