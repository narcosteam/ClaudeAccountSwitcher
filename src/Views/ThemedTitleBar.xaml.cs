using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeAccountSwitcher;

// ponytail: WindowStyle="None" gives us a blank canvas, so we own the
// drag/minimize/close chrome ourselves instead of fighting the OS title bar's
// colors. Shared across MainWindow/AddAccountWindow/RenameAccountWindow so
// the drag+button plumbing lives in exactly one place.
public partial class ThemedTitleBar : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ThemedTitleBar), new PropertyMetadata(""));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowMinimize
    {
        set => MinimizeButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public ThemedTitleBar()
    {
        InitializeComponent();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            Window.GetWindow(this)?.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is not null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
