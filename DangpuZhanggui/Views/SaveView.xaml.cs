using System.Windows;
using System.Windows.Controls;
using Dangpu.Services;

namespace Dangpu.Views;

public partial class SaveView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public SaveView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() => _vm.RefreshSaves();

    private void Create_Click(object sender, RoutedEventArgs e) => _vm.CreateSaveSnapshot();

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SaveSnapshot snapshot }) return;

        var confirmed = MessageDialog.Confirm("还原存档",
            $"把存档还原到 {snapshot.CreatedText} 的状态？{Environment.NewLine}{Environment.NewLine}" +
            "会覆盖当前的 SaveFile.es3、saves_index.es3 和快照里的那个存档文件。还原前会自动再拍一张。",
            "还原", danger: true);

        if (confirmed) _vm.RestoreSaveSnapshot(snapshot);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SaveSnapshot snapshot }) return;

        if (MessageDialog.Confirm("删除存档快照",
                $"删除 {snapshot.CreatedText} 的快照？{Environment.NewLine}{Environment.NewLine}只删快照，不影响游戏存档。",
                "删除", danger: true))
            _vm.DeleteSaveSnapshot(snapshot);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _vm.RefreshSaves();

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _vm.OpenSaveFolder();
}
