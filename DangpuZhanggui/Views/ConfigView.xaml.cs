using System.Windows;
using System.Windows.Controls;
using Dangpu.Services;

namespace Dangpu.Views;

public partial class ConfigView : UserControl, IPage
{
    private readonly MainViewModel _vm;
    private ConfigSection? _section;

    public ConfigView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() =>
        Dispatcher.BeginInvoke(new Action(SelectFirstSection), System.Windows.Threading.DispatcherPriority.Loaded);

    private void SelectFirstSection()
    {
        if (SectionList.Items.Count == 0) return;
        if (SectionList.SelectedItem is ConfigSection) return;

        var first = _vm.Config.Sections.FirstOrDefault(s => s.Entries.Any(e => e.IsEditable))
                    ?? _vm.Config.Sections.FirstOrDefault(s => s.Entries.Count > 0)
                    ?? _vm.Config.Sections.FirstOrDefault();
        SectionList.SelectedItem = first;
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionList.SelectedItem is not ConfigSection section) return;
        _section = section;

        if (!string.IsNullOrEmpty(ConfigSearchBox.Text))
        {
            ConfigSearchBox.Text = "";
            return;
        }

        ApplyConfigFilter();
    }

    private void ConfigSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyConfigFilter();

    private void ApplyConfigFilter()
    {
        if (_vm == null) return;

        var query = ConfigSearchBox.Text.Trim();

        if (query.Length == 0)
        {
            if (_section == null)
            {
                EntryList.ItemsSource = null;
                SectionTitle.Text = "选择一个分组";
                SectionHint.Text = "";
                return;
            }

            EntryList.ItemsSource = _section.Entries;
            SectionTitle.Text = _section.DisplayName;
            SectionHint.Text = $"{_section.Entries.Count} 项";
            return;
        }

        var results = _vm.Config.AllEntries.Where(entry =>
            entry.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Comment.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Value.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        EntryList.ItemsSource = results;
        SectionTitle.Text = "搜索结果";
        SectionHint.Text = $"{results.Count} 项";
    }

    private void Save_Click(object sender, RoutedEventArgs e) => _vm.SaveConfig();

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Config.IsDirty)
        {
            if (!MessageDialog.Confirm("还原配置", "放弃还没保存的改动，从文件重新读一遍？", "还原")) return;
        }

        _vm.ReloadConfig();
        ConfigSearchBox.Text = "";
        _section = null;
        SectionList.SelectedItem = null;
        SelectFirstSection();
        _vm.SetStatus("配置已重新读取", StatusKind.Info);
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e) => _vm.OpenConfigFile();
}
