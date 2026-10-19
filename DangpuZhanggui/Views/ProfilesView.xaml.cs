using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Dangpu.Services;

namespace Dangpu.Views;

public partial class ProfilesView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public ProfilesView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() => _vm.RefreshProfiles();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NameDialog("保存方案", "给这套启停组合起个名字", $"方案 {DateTime.Now:MM-dd HH:mm}")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true) _vm.SaveProfile(dialog.Value);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProfileItem item }) return;

        var confirmed = MessageDialog.Confirm("应用方案",
            $"把「{item.Title}」的开关状态套到当前？{Environment.NewLine}{Environment.NewLine}" +
            "方案里记录过的模组会改成当时的状态，没记录的不动。",
            "应用");

        if (confirmed) _vm.ApplyProfile(item, false);
    }

    private void ApplyStrict_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProfileItem item }) return;

        var confirmed = MessageDialog.Confirm("严格应用方案",
            $"严格应用「{item.Title}」？{Environment.NewLine}{Environment.NewLine}" +
            "方案里没记录过的模组也会被停用。这套方案之后如果装了新模组，切回去时它们会被关掉。",
            "严格应用", danger: true);

        if (confirmed) _vm.ApplyProfile(item, true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProfileItem item }) return;

        var confirmed = MessageDialog.Confirm("删除方案",
            $"删除方案「{item.Title}」？{Environment.NewLine}{Environment.NewLine}只删记录，不影响模组文件。",
            "删除", danger: true);

        if (confirmed) _vm.DeleteProfile(item);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _vm.RefreshProfiles();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ProfileService.ProfileDir}\"") { UseShellExecute = true });
        }
        catch { }
    }
}
