using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dangpu.Services;
using Microsoft.Win32;

namespace Dangpu.Views;

public partial class ModpacksView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public ModpacksView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() => _vm.RefreshModpacks();

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(_vm.PackName) ? "Probably Stolen 整合包" : _vm.PackName.Trim();
        var safe = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        var dialog = new SaveFileDialog
        {
            Title = "保存整合包",
            FileName = safe + ".dspmp",
            DefaultExt = ".dspmp",
            AddExtension = true,
            Filter = "Deep Space 整合包 (*.dspmp)|*.dspmp|Zip (*.zip)|*.zip"
        };

        if (dialog.ShowDialog() != true) return;
        await _vm.CreateModpackAsync(dialog.FileName);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入整合包",
            Filter = "Deep Space 整合包 (*.dspmp;*.zip)|*.dspmp;*.zip|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true) _vm.ImportModpack(dialog.FileName);
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedModpack is not { } pack) return;
        var current = $"{_vm.Game.Version} / build {_vm.Game.BuildId}";
        var packGame = $"{pack.GameVersion} / build {pack.BuildId}";
        var versionLine = string.Equals(current, packGame, StringComparison.OrdinalIgnoreCase)
            ? ""
            : $"\n整合包是 {packGame}，当前游戏是 {current}。\n";

        var message = $"安装「{pack.Name}」会：\n\n" +
                      "1. 自动备份当前 Mods 和 MelonPreferences.cfg\n" +
                      "2. 清空 Mods 文件夹\n" +
                      "3. 解压整合包里的模组\n" +
                      versionLine +
                      "\n游戏必须先关闭。只安装你信任的整合包，里面的 DLL 会随游戏加载。继续？";
        if (MessageDialog.Confirm("安装整合包", message, "安装"))
            await _vm.InstallSelectedModpackAsync();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedModpack is not { } pack) return;
        if (MessageDialog.Confirm("删除整合包", $"从本地整合包文件夹删除「{pack.Name}」？只会删除库里的副本，不会动你另存到别处的文件。", "删除"))
            _vm.DeleteSelectedModpack();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedModpack is not { } pack) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{pack.Path}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _vm.OpenModpackFolder();
}
