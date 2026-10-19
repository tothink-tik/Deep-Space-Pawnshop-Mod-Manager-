using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using Dangpu.Services;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dangpu.Views;

namespace Dangpu;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _dropTimer;
    private readonly Dictionary<int, FrameworkElement> _pages = new();
    private string _dropInfo = "";

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        ThemeMetrics.SetUiStyle(this, vm.Settings.UiStyle);

        SourceInitialized += OnSourceInitialized;
        vm.PropertyChanged += OnViewModelChanged;
        StateChanged += (_, _) => UpdateChrome();

        ShowPage(Math.Clamp(vm.PageIndex, 0, 9));
        ThemeMetrics.Current.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThemeMetrics.CardRadius))
                Dispatcher.BeginInvoke(new Action(() => ThemeService.ApplyRadii(PageHost.Content as DependencyObject)),
                    System.Windows.Threading.DispatcherPriority.Loaded);
        };
        UpdateChrome();

        _dropTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
        _dropTimer.Tick += (_, _) =>
        {
            _dropTimer.Stop();
            HideDropOverlay();
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) =>
        {
            vm.Tick();
            if (vm.PageIndex == 2 && vm.Settings.AutoRefreshLog) vm.RefreshLog();
            if (vm.PageIndex == 1) vm.Notify(nameof(vm.SelectedSummary));
        };
        _timer.Start();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PageIndex))
            ShowPage(Math.Clamp(_vm.PageIndex, 0, 9));
    }

    internal ContentControl Host => PageHost;
    internal FrameworkElement? CurrentPage => PageHost.Content as FrameworkElement;

    private void ShowPage(int index)
    {
        if (!_pages.TryGetValue(index, out var page))
        {
            page = index switch
            {
                1 => new ModsView(_vm),
                2 => new LogsView(_vm),
                3 => new ConfigView(_vm),
                4 => new ProfilesView(_vm),
                5 => new BackupsView(_vm),
                6 => new SaveView(_vm),
                7 => new DiagnosisView(_vm),
                8 => new DiscoverView(_vm),
                9 => new SettingsView(_vm),
                _ => new OverviewView(_vm)
            };
            _pages[index] = page;
        }

        PageHost.Content = page;
        Dispatcher.BeginInvoke(new Action(() => ThemeService.ApplyRadii(page)),
            System.Windows.Threading.DispatcherPriority.Loaded);
        (page as IPage)?.OnActivated();

        // 页面入场：淡入 + 轻微上移，180ms
        page.Opacity = 0;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        page.BeginAnimation(OpacityProperty, fade);

        var slide = new TranslateTransform(0, 6);
        page.RenderTransform = slide;
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    // ---------------- 窗口边框 / 拖动 ----------------

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
        catch { }
    }

    private static bool IsInsideButton(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is Button) return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MusicToggle_Click(object sender, RoutedEventArgs e) => _vm.ToggleMusic();

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateChrome()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaxIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        RootBorder.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

        try
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var work = info.rcWork;
                var screen = info.rcMonitor;
                mmi.ptMaxPosition.x = work.left - screen.left;
                mmi.ptMaxPosition.y = work.top - screen.top;
                mmi.ptMaxSize.x = work.right - work.left;
                mmi.ptMaxSize.y = work.bottom - work.top;
                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
        }
        catch { }

        return IntPtr.Zero;
    }

    // ---------------- 拖入安装 ----------------

    private void Launch_Click(object sender, RoutedEventArgs e) => _vm.LaunchGame();

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        var hasFiles = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (!hasFiles)
        {
            HideDropOverlay();
            return;
        }

        ShowDropOverlay(e.Data.GetData(DataFormats.FileDrop) as string[]);
        _dropTimer.Stop();
        _dropTimer.Start();
    }

    // 拖过子控件时 DragLeave 也会触发，所以用一个小定时器判断是不是真的离开了窗口
    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        _dropTimer.Stop();
        _dropTimer.Start();
    }

    internal void ShowDropOverlayForTest(string[] paths) => ShowDropOverlay(paths);

    internal void HideDropOverlayForTest() => HideDropOverlay();

    private void ShowDropOverlay(string[]? paths)
    {
        if (paths is { Length: > 0 })
        {
            var names = paths
                .Select(p => System.IO.Path.GetFileName(p.TrimEnd('\\', '/')))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Take(3)
                .ToList();

            var text = paths.Length == 1
                ? names.FirstOrDefault() ?? ""
                : $"{paths.Length} 项：{string.Join("、", names)}" + (paths.Length > 3 ? " …" : "");

            if (text != _dropInfo)
            {
                _dropInfo = text;
                DropInfo.Text = text;
            }
        }

        if (DropOverlay.Visibility == Visibility.Visible) return;

        DropOverlay.Visibility = Visibility.Visible;
        DropOverlay.Opacity = 0;
        DropOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

        DropScale.ScaleX = DropScale.ScaleY = 0.97;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
        DropScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        DropScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    private void HideDropOverlay()
    {
        if (DropOverlay.Visibility != Visibility.Visible) return;
        DropOverlay.Visibility = Visibility.Collapsed;
        DropOverlay.Opacity = 1;
        DropScale.ScaleX = DropScale.ScaleY = 0.97;
        _dropInfo = "";
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        _dropTimer.Stop();
        HideDropOverlay();

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _vm.InstallPaths(paths);
    }
}
