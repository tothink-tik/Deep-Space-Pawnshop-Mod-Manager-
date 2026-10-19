using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Dangpu.Services;

/// <summary>跨样式共享的尺寸/可见性，绑定到全局样式上。</summary>
public sealed class ThemeMetrics : INotifyPropertyChanged
{
    public static ThemeMetrics Current { get; } = new();

    public static readonly DependencyProperty UiStyleProperty = DependencyProperty.RegisterAttached(
        "UiStyle", typeof(string), typeof(ThemeMetrics), new PropertyMetadata("terminal"));

    public static string GetUiStyle(DependencyObject d) => (string)d.GetValue(UiStyleProperty);
    public static void SetUiStyle(DependencyObject d, string value) => d.SetValue(UiStyleProperty, value);

    private CornerRadius _cardRadius = new(2);
    private CornerRadius _controlRadius = new(2);
    private CornerRadius _rowRadius = new(2);
    private CornerRadius _navRadius = new(0);
    private Visibility _hudVisibility = Visibility.Visible;

    public CornerRadius CardRadius
    {
        get => _cardRadius;
        set => Set(ref _cardRadius, value);
    }

    public CornerRadius ControlRadius
    {
        get => _controlRadius;
        set => Set(ref _controlRadius, value);
    }

    public CornerRadius RowRadius
    {
        get => _rowRadius;
        set => Set(ref _rowRadius, value);
    }

    public CornerRadius NavRadius
    {
        get => _navRadius;
        set => Set(ref _navRadius, value);
    }

    public Visibility HudVisibility
    {
        get => _hudVisibility;
        set => Set(ref _hudVisibility, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
