using System.Windows;
using System.Windows.Input;

namespace Dangpu.Views;

public partial class NameDialog : Window
{
    public string Value => NameBox.Text.Trim();

    public NameDialog(string title, string hint, string initial)
    {
        InitializeComponent();
        TitleText.Text = title;
        HintText.Text = hint;
        NameBox.Text = initial;

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) DialogResult = false;
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { }
    }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Confirm();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Value)) return;
        DialogResult = true;
    }
}
