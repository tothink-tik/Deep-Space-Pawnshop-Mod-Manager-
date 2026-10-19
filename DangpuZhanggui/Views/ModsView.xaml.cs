using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Dangpu.Models;
using Microsoft.Win32;

namespace Dangpu.Views;

public partial class ModsView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public ModsView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        SyncFilterChips();
    }

    public void OnActivated()
    {
        _vm.RefreshLog();
        SyncFilterChips();
        if (_vm.SelectedMod == null)
            _vm.SelectedMod = _vm.Mods.FirstOrDefault(m => m.Enabled) ?? _vm.Mods.FirstOrDefault();
    }

    private void SyncFilterChips()
    {
        ChipAll.IsChecked = _vm.Filter == ModFilter.All;
        ChipEnabled.IsChecked = _vm.Filter == ModFilter.Enabled;
        ChipDisabled.IsChecked = _vm.Filter == ModFilter.Disabled;
        ChipProblem.IsChecked = _vm.Filter == ModFilter.Problem;
    }

    private void ModList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModList.SelectedItem != null) ModList.ScrollIntoView(ModList.SelectedItem);
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton chip || chip.Tag is not string tag) return;
        _vm.Filter = tag switch
        {
            "Enabled" => ModFilter.Enabled,
            "Disabled" => ModFilter.Disabled,
            "Problem" => ModFilter.Problem,
            _ => ModFilter.All
        };
        SyncFilterChips();
    }

    private void ModCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && box.DataContext is ModItem mod)
            _vm.ToggleMod(mod, box.IsChecked == true);
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedMod is { } mod) _vm.ToggleMod(mod, !mod.Enabled);
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要安装的模组",
            Filter = "模组文件 (*.dll;*.zip)|*.dll;*.zip|所有文件 (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true) _vm.InstallPaths(dialog.FileNames);
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择放着模组的文件夹（会递归找 dll 和 zip）" };
        if (dialog.ShowDialog() == true) _vm.InstallPaths(new[] { dialog.FolderName });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _vm.OpenModsFolder();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出详细模组列表",
            Filter = "Markdown (*.md)|*.md|CSV 表格 (*.csv)|*.csv",
            FileName = $"当铺模组列表-{DateTime.Now:yyyyMMdd}.md",
            AddExtension = true
        };

        if (dialog.ShowDialog() != true) return;

        var path = dialog.FileName;
        var wantMarkdown = dialog.FilterIndex == 1;
        if (wantMarkdown && !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            path = System.IO.Path.ChangeExtension(path, ".md");
        else if (!wantMarkdown && !path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            path = System.IO.Path.ChangeExtension(path, ".csv");

        _vm.ExportModList(path);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e) => _vm.OpenModFolder(_vm.SelectedMod);

    private void OpenLink_Click(object sender, RoutedEventArgs e) => _vm.OpenModLink();

    private void ToggleDescription_Click(object sender, RoutedEventArgs e) => _vm.ToggleDescription();

    private void OpenNexusSettings_Click(object sender, RoutedEventArgs e) => _vm.GoToSettings();

    private void Batch_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageDialog.Ask("批量操作", "把当前所有模组一起启用还是停用？",
            "全部启用", "全部停用", "取消");

        if (answer == DialogAnswer.Yes) _vm.SetAllMods(true);
        else if (answer == DialogAnswer.No) _vm.SetAllMods(false);
    }

    private void Sort_Click(object sender, RoutedEventArgs e) => _vm.CycleSort();

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await _vm.CheckUpdatesAsync();

    private void BindNexus_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NameDialog("N 网页面",
            "粘贴模组的 N 网地址，例如 https://www.nexusmods.com/probablystolen/mods/66。",
            _vm.SelectedMod?.NexusLink ?? "")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true) return;

        if (!_vm.BindNexusLink(dialog.Value))
            MessageDialog.Info("绑定失败", "这不像 N 网模组地址。地址里要有 /mods/数字。", DialogTone.Warning);
    }

    private void UnbindNexus_Click(object sender, RoutedEventArgs e) => _vm.BindNexusLink(null);

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedMod is { } mod) _vm.DeleteMod(mod);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _vm.Rescan();

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        _vm.SearchText = "";
        _vm.Filter = ModFilter.All;
        SyncFilterChips();
    }
}
