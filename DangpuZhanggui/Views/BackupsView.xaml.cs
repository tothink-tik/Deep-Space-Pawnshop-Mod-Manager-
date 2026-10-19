using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dangpu.Services;

namespace Dangpu.Views;

public partial class BackupsView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public BackupsView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated() => _vm.RefreshBackups();

    private void Create_Click(object sender, RoutedEventArgs e) => _vm.CreateBackup();

    private void Refresh_Click(object sender, RoutedEventArgs e) => _vm.RefreshBackups();

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BackupItem item }) _vm.RestoreBackup(item);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BackupItem item }) _vm.DeleteBackup(item);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{BackupService.BackupDir}\"") { UseShellExecute = true });
        }
        catch { }
    }
}
