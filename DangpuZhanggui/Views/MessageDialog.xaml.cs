using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Dangpu.Views;

public enum DialogAnswer { Yes, No, Cancel }

public enum DialogTone { Info, Warning, Danger }

public partial class MessageDialog : Window
{
    public DialogAnswer Answer { get; private set; } = DialogAnswer.Cancel;

    internal MessageDialog(string title, string message, string yesText, string yesGlyph,
        string? noText, string? cancelText, DialogTone tone)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        YesButton.Content = yesText;
        IconGlyph.Text = yesGlyph;

        NoButton.Content = noText ?? "取消";
        NoButton.Visibility = noText == null ? Visibility.Collapsed : Visibility.Visible;

        CancelButton.Content = cancelText ?? "取消";
        CancelButton.Visibility = cancelText == null ? Visibility.Collapsed : Visibility.Visible;

        if (tone == DialogTone.Danger)
        {
            YesButton.Style = (Style)FindResource("BtnDangerSolid");
            IconBorder.Background = (Brush)FindResource("DangerSoftBrush");
            IconGlyph.Foreground = (Brush)FindResource("DangerBrush");
        }
        else if (tone == DialogTone.Warning)
        {
            IconBorder.Background = (Brush)FindResource("WarnSoftBrush");
            IconGlyph.Foreground = (Brush)FindResource("WarnBrush");
        }

        Loaded += (_, _) => YesButton.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Answer = DialogAnswer.Cancel;
            DialogResult = false;
        };
    }

    /// <summary>是 / 否。返回 true 表示点了“是”。</summary>
    public static bool Confirm(string title, string message, string yesText = "确定", bool danger = false)
    {
        var dialog = new MessageDialog(title, message, yesText, danger ? "!" : "?", "取消", null,
            danger ? DialogTone.Danger : DialogTone.Info)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
        return dialog.Answer == DialogAnswer.Yes;
    }

    /// <summary>三个选项，用于“是 / 否 / 取消”这种。</summary>
    public static DialogAnswer Ask(string title, string message, string yesText, string noText, string cancelText)
    {
        var dialog = new MessageDialog(title, message, yesText, "?", noText, cancelText, DialogTone.Info)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
        return dialog.Answer;
    }

    /// <summary>只有一个“知道了”。</summary>
    public static void Info(string title, string message, DialogTone tone = DialogTone.Info)
    {
        var dialog = new MessageDialog(title, message, "知道了", tone == DialogTone.Danger ? "!" : "i", null, null, tone)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Answer = DialogAnswer.Yes;
        DialogResult = true;
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Answer = DialogAnswer.No;
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Answer = DialogAnswer.Cancel;
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { }
    }
}
