using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Dangpu.Views;

public partial class LogsView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public LogsView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() => _vm.RefreshLog();

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshLog();
        _vm.SetStatus("日志已刷新", StatusKind.Info);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _vm.OpenLogFolder();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出日志",
            Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt",
            FileName = $"Latest-{DateTime.Now:yyyyMMdd-HHmm}.log"
        };

        if (dialog.ShowDialog() == true) _vm.ExportLog(dialog.FileName);
    }
}
