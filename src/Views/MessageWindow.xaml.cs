using System.Windows;

namespace ClaudeAccountSwitcher;

// Replaces MessageBox.Show — matches the app's own styling instead of the native dialog.
public partial class MessageWindow : Window
{
    private bool _confirmed;

    public MessageWindow()
    {
        InitializeComponent();
    }

    public static void ShowInfo(Window? owner, string title, string message) =>
        Create(owner, title, message, showCancel: false, confirmLabel: "OK").ShowDialog();

    public static void ShowError(Window? owner, string title, string message) =>
        Create(owner, title, message, showCancel: false, confirmLabel: "OK").ShowDialog();

    public static bool Confirm(Window? owner, string title, string message, string confirmLabel = "Yes")
    {
        var window = Create(owner, title, message, showCancel: true, confirmLabel: confirmLabel);
        window.ShowDialog();
        return window._confirmed;
    }

    private static MessageWindow Create(Window? owner, string title, string message, bool showCancel, string confirmLabel)
    {
        var window = new MessageWindow { Owner = owner };
        window.TitleBar.Title = title;
        window.MessageText.Text = message;
        window.CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        window.ConfirmButton.Content = confirmLabel;
        return window;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = false;
        Close();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }
}
