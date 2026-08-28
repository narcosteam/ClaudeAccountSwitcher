using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeAccountSwitcher;

// WindowStyle="None" means we own drag/minimize/close ourselves; shared across all windows.
public partial class ThemedTitleBar : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ThemedTitleBar), new PropertyMetadata(""));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // Default 0 (square) matches MainWindow, which has no rounded outer border of its own.
    // Dialogs wrapped in a rounded Border set this to match it — otherwise this control's
    // own square top corners paint over (and cut into) that Border's rounded top stroke.
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ThemedTitleBar), new PropertyMetadata(new CornerRadius(0)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
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
