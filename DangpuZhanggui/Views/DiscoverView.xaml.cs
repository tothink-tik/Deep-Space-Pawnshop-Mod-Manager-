using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dangpu.Models;

namespace Dangpu.Views;

public partial class DiscoverView : UserControl, IPage
{
    private readonly MainViewModel _vm;

    public DiscoverView(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public void OnActivated()
    {
        _vm.RefreshSearchAvailability();
        Dispatcher.BeginInvoke(new Action(() => SearchBox.Focus()), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await _vm.SearchNexusAsync(false);

    private async void Search_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await _vm.SearchNexusAsync(false);
    }

    private async void LoadMore_Click(object sender, RoutedEventArgs e) => await _vm.SearchNexusAsync(true);

    private void Sort_Click(object sender, RoutedEventArgs e) => _vm.CycleSearchSort();

    private void OpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: NexusSearchResult result }) _vm.OpenSearchResult(result);
    }

    private void OpenKeySettings_Click(object sender, RoutedEventArgs e) => _vm.GoToNexusKeySettings();

    private void OpenNexus_Click(object sender, RoutedEventArgs e) => _vm.OpenNexusSearchPage();

    private async void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string query }) return;
        _vm.SearchQuery = query;
        await _vm.SearchNexusAsync(false);
    }
}
