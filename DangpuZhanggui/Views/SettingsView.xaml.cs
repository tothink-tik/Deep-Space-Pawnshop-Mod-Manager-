using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dangpu.Services;
using Microsoft.Win32;

namespace Dangpu.Views;

public partial class SettingsView : UserControl, IPage
{
    private readonly MainViewModel _vm;
    private bool _loading;

    public SettingsView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => ReloadFields();
    }

    public void OnActivated()
    {
        ReloadFields();
        _vm.RefreshLoader();
    }

    private void ReloadFields()
    {
        _loading = true;
        PathBox.Text = _vm.Settings.GamePath ?? _vm.Game.GameDir ?? "";
        ArgsBox.Text = _vm.Settings.ExtraArgs;
        NexusKeyBox.Password = _vm.Settings.NexusApiKey;

        var accent = _vm.Settings.Accent;
        foreach (var radio in Swatches.Children.OfType<RadioButton>())
            radio.IsChecked = string.Equals(radio.Tag as string, accent, StringComparison.OrdinalIgnoreCase);

        StyleTerminal.IsChecked = _vm.Settings.UiStyle == "terminal";
        StyleNight.IsChecked = _vm.Settings.UiStyle == "night";
        StyleGraphite.IsChecked = _vm.Settings.UiStyle == "graphite";

        _loading = false;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 Probably Stolen.exe 所在的文件夹" };
        if (dialog.ShowDialog() == true)
        {
            PathBox.Text = dialog.FolderName;
            _vm.SetGamePath(dialog.FolderName);
        }
    }

    private void ApplyPath_Click(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text.Trim();
        if (path.Length == 0) return;
        _vm.SetGamePath(path);
        ReloadFields();
    }

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        _vm.AutoDetectGame();
        ReloadFields();
    }

    private void OpenGame_Click(object sender, RoutedEventArgs e) => _vm.OpenGameFolder();

    private void Option_Click(object sender, RoutedEventArgs e)
    {
        _vm.Settings.Save();
        _vm.SetStatus("启动选项已保存", StatusKind.Info);
    }

    private void SaveArgs_Click(object sender, RoutedEventArgs e)
    {
        _vm.Settings.ExtraArgs = ArgsBox.Text.Trim();
        _vm.Settings.Save();
        _vm.SetStatus("附加参数已保存", StatusKind.Info);
    }

    private void UiStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string style }) return;
        _vm.SetUiStyle(style);
        ReloadFields();
    }

    private void Accent_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton { Tag: string hex }) return;
        _vm.ApplyAccent(hex);
        _vm.SetStatus("主题色已切换", StatusKind.Info);
    }

    private async void InstallLoader_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.LoaderBusy) return;

        var useCache = _vm.Loader.CacheExists;
        if (useCache)
        {
            var answer = MessageDialog.Ask("安装 MelonLoader",
                $"本地已经缓存了安装包（{_vm.Loader.CacheSize / 1024.0 / 1024.0:0.0} MB）。" + Environment.NewLine + Environment.NewLine +
                "「用缓存的」不动网络；「重新下载」会拉最新版。",
                "用缓存的", "重新下载", "取消");
            if (answer == DialogAnswer.Cancel) return;
            useCache = answer == DialogAnswer.Yes;
        }

        await _vm.InstallLoaderAsync(useCache);
        ReloadFields();
    }

    private void InstallLoaderZip_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 MelonLoader 安装包",
            Filter = "MelonLoader 安装包 (*.zip)|*.zip|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true) _vm.InstallLoaderFromZip(dialog.FileName);
    }

    private void ExportLoader_Click(object sender, RoutedEventArgs e)
    {
        var version = string.IsNullOrWhiteSpace(_vm.Loader.Version) ? "" : "-" + _vm.Loader.Version;
        var dialog = new SaveFileDialog
        {
            Title = "导出当前加载器",
            Filter = "压缩包 (*.zip)|*.zip",
            FileName = $"MelonLoader-x64{version}.zip"
        };
        if (dialog.ShowDialog() == true) _vm.ExportLoader(dialog.FileName);
    }

    private void OpenLoaderFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = _vm.Game.GameDir == null ? null : Path.Combine(_vm.Game.GameDir, "MelonLoader");
        GameRunner.OpenInExplorer(Directory.Exists(folder) ? folder : _vm.Game.GameDir);
    }

    private void OpenLoaderPage_Click(object sender, RoutedEventArgs e) => _vm.OpenLoaderDownloadPage();

    private void CancelLoader_Click(object sender, RoutedEventArgs e) => _vm.CancelLoader();

    private void LoaderToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.LoaderBusy) return;
        _vm.SetLoaderEnabled(!_vm.LoaderEnabled);
    }

    private void SaveNexusKey_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveNexusKey(NexusKeyBox.Password);
        ReloadFields();
    }

    private async void TestNexusKey_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveNexusKey(NexusKeyBox.Password);
        await _vm.TestNexusKeyAsync();
    }

    private void OpenNexusKeyPage_Click(object sender, RoutedEventArgs e) => _vm.OpenNexusKeyPage();

    private void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择背景图",
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true) _vm.SetBackground(dialog.FileName);
    }

    private void ClearBackground_Click(object sender, RoutedEventArgs e)
    {
        if (MessageDialog.Confirm("清除背景", "会删除复制到工具数据目录里的背景图，恢复默认深色背景。", "清除"))
            _vm.ClearBackground();
    }

    private void AddMusic_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "添加背景音乐",
            Multiselect = true,
            Filter = "音频 (*.mp3;*.wav;*.wma;*.m4a;*.aac)|*.mp3;*.wav;*.wma;*.m4a;*.aac|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true) _vm.AddMusicFiles(dialog.FileNames);
    }

    private void ToggleMusic_Click(object sender, RoutedEventArgs e) => _vm.ToggleMusic();

    private void PreviousMusic_Click(object sender, RoutedEventArgs e) => _vm.PreviousMusic();

    private void NextMusic_Click(object sender, RoutedEventArgs e) => _vm.NextMusic();

    private void RemoveMusic_Click(object sender, RoutedEventArgs e) => _vm.RemoveSelectedMusic();

    private void ClearMusic_Click(object sender, RoutedEventArgs e)
    {
        if (MessageDialog.Confirm("清空背景音乐", "会停止播放，并删除复制到工具数据目录里的音乐文件。", "清空"))
            _vm.ClearMusic();
    }

    private void MusicList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => _vm.PlaySelectedMusic();

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppSettings.DataDir}\"") { UseShellExecute = true });
        }
        catch { }
    }
}
