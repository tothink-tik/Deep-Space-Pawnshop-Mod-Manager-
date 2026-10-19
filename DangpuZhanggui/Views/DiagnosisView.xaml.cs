using System.Windows;
using System.Windows.Controls;

namespace Dangpu.Views;

public partial class DiagnosisView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public DiagnosisView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() => _vm.RefreshDiagnostics();

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshDiagnostics();
        _vm.SetStatus("诊断已重新分析", StatusKind.Info);
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e) => _vm.PageIndex = 2;

    private void SafeLaunch_Click(object sender, RoutedEventArgs e) => _vm.SafeLaunch();

    private void Suspect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string name }) _vm.SelectModByName(name);
    }
}
