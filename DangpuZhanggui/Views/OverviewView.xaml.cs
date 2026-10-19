using System.Windows;
using System.Windows.Controls;

namespace Dangpu.Views;

public partial class OverviewView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public OverviewView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated()
    {
        _vm.RefreshLog();
        _vm.RefreshBackups();
    }

    private void Launch_Click(object sender, RoutedEventArgs e) => _vm.LaunchGame();

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _vm.OpenGameFolder();

    private void OpenLog_Click(object sender, RoutedEventArgs e) => _vm.PageIndex = 2;

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.Rescan(silent: true);
        _vm.RefreshGameInfo();
        _vm.RefreshLog();
        _vm.RefreshBackups();
        _vm.SetStatus("已刷新", StatusKind.Info);
    }

    private void Detect_Click(object sender, RoutedEventArgs e) => _vm.AutoDetectGame();
}
