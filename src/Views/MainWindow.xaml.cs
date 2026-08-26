using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeAccountSwitcher;

public partial class MainWindow : Window
{
    private readonly App _app;
    private const double BarWidth = 200;

    public MainWindow(App app)
    {
        InitializeComponent();
        _app = app;
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // ponytail: tray app — X minimizes to tray like the rest of this
        // app's windows-as-tray-icon peers (eve-o-preview etc.), it doesn't
        // quit. Only the tray's own Exit sets IsExiting first.
        if (!_app.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void SetBusy(bool busy, string? message = null)
    {
        BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        BusyText.Text = message ?? "";
    }

    public void RefreshAccounts()
    {
        AccountsPanel.Children.Clear();
        var activeId = _app.AccountStore.GetActiveAccountId();

        foreach (var entry in _app.AccountStore.ListAccounts())
        {
            var needsReauth = _app.AccountStore.LoadAccount(entry.Id) is null || _app.NeedsReauth(entry.Id);
            AccountsPanel.Children.Add(BuildAccountRow(entry, entry.Id == activeId, needsReauth, _app.GetCachedUsage(entry.Id)));
        }
    }

    private FrameworkElement BuildAccountRow(AccountIndexEntry entry, bool isActive, bool needsReauth, UsageInfo? usage)
    {
        var row = new Border
        {
            Background = (Brush)FindResource("RowBackgroundBrush"),
            BorderBrush = (Brush)FindResource(isActive ? "BarFillNormalBrush" : "RowBorderBrush"),
            BorderThickness = new Thickness(isActive ? 1.5 : 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = needsReauth ? 0.6 : 1,
            Cursor = needsReauth ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand,
        };

        var content = new StackPanel();
        content.Children.Add(BuildTopRow(entry));

        if (needsReauth)
        {
            var reauthText = new TextBlock
            {
                Text = "Needs re-authorization",
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 6),
                Foreground = (Brush)FindResource("BarFillWarningBrush"),
            };
            content.Children.Add(reauthText);

            var signInButton = new Button
            {
                Content = "Sign in again",
                Style = (Style)FindResource("PrimaryButtonStyle"),
                Padding = new Thickness(0, 6, 0, 6),
            };
            signInButton.Click += (_, e) =>
            {
                e.Handled = true;
                OpenAddAccountWindow();
            };
            content.Children.Add(signInButton);
        }
        else
        {
            content.Children.Add(BuildUsageRow("5h", usage?.FiveHour));
            content.Children.Add(BuildUsageRow("7d", usage?.SevenDay));

            row.MouseLeftButtonUp += (_, _) => SwitchTo(entry.Id);
        }

        row.Child = content;
        return row;
    }

    private FrameworkElement BuildTopRow(AccountIndexEntry entry)
    {
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = (Brush)FindResource("AvatarBackgroundBrush"),
            Margin = new Thickness(0, 0, 8, 0),
        };
        avatar.Child = new TextBlock
        {
            Text = entry.Label.Length > 0 ? entry.Label[..1].ToUpperInvariant() : "?",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AppBackgroundBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(avatar, 0);
        top.Children.Add(avatar);

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var nameText = new TextBlock
        {
            Text = entry.Label,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("AppForegroundBrush"),
        };
        if (!string.IsNullOrEmpty(entry.Email))
        {
            nameText.ToolTip = entry.Email;
        }
        nameRow.Children.Add(nameText);

        var badge = new Border
        {
            Background = (Brush)FindResource(entry.IsTeamAccount ? "BadgeTeamBackgroundBrush" : "BadgePersonalBackgroundBrush"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 1, 7, 1),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock
        {
            Text = entry.IsTeamAccount ? "TEAM" : "PERSONAL",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource(entry.IsTeamAccount ? "BadgeTeamForegroundBrush" : "BadgePersonalForegroundBrush"),
        };
        nameRow.Children.Add(badge);
        Grid.SetColumn(nameRow, 1);
        top.Children.Add(nameRow);

        var renameButton = BuildIconButton("✎"); // pencil
        renameButton.Click += (_, e) =>
        {
            e.Handled = true;
            new RenameAccountWindow(_app.AccountStore, entry.Id, entry.Label) { Owner = this }.ShowDialog();
            RefreshAccounts();
        };
        Grid.SetColumn(renameButton, 2);
        top.Children.Add(renameButton);

        var moreButton = BuildIconButton("⋯"); // three dots
        moreButton.Click += (_, e) =>
        {
            e.Handled = true;
            ShowAccountMenu(moreButton, entry);
        };
        Grid.SetColumn(moreButton, 3);
        top.Children.Add(moreButton);

        return top;
    }

    private Button BuildIconButton(string glyph)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 24,
            Height = 24,
            Margin = new Thickness(4, 0, 0, 0),
            Style = (Style)FindResource("IconButtonStyle"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return button;
    }

    private void ShowAccountMenu(FrameworkElement anchor, AccountIndexEntry entry)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, IsOpen = true };
        var signOutItem = new MenuItem { Header = "Sign out" };
        signOutItem.Click += (_, _) => SignOut(entry);
        menu.Items.Add(signOutItem);
    }

    private void SignOut(AccountIndexEntry entry)
    {
        if (!MessageWindow.Confirm(this, "Sign out", $"Remove \"{entry.Label}\" from the switcher?", "Remove"))
        {
            return;
        }
        _app.Switcher.SignOut(entry.Id);
        RefreshAccounts();
    }

    private void SwitchTo(string accountId)
    {
        try
        {
            _app.Switcher.SwitchTo(accountId);
        }
        catch (Exception ex) // ponytail: SwitchTo can also throw JsonException/IOException, not just InvalidOperationException — catch broadly per design spec's "no crash on write failure"
        {
            MessageWindow.ShowError(this, "Couldn't switch account", ex.Message);
        }
        RefreshAccounts();
    }

    private void AddAccountButton_Click(object sender, RoutedEventArgs e) => OpenAddAccountWindow();

    private void OpenAddAccountWindow()
    {
        new AddAccountWindow(_app.TokenEndpoint, _app.ProfileClient, _app.AccountStore) { Owner = this }.ShowDialog();
        RefreshAccounts();
    }

    private FrameworkElement BuildUsageRow(string windowLabel, RateLimitWindow? window)
    {
        var container = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

        var track = new Border
        {
            Width = BarWidth,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = (Brush)FindResource("BarTrackBrush"),
            Margin = new Thickness(0, 0, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (window is not null)
        {
            var fillBrushKey = window.UsedPercentage >= 80 ? "BarFillWarningBrush" : "BarFillNormalBrush";
            track.Child = new Border
            {
                Width = BarWidth * Math.Clamp(window.UsedPercentage, 0, 100) / 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(2),
                Background = (Brush)FindResource(fillBrushKey),
            };
        }
        container.Children.Add(track);

        var labelRow = new Grid();
        labelRow.ColumnDefinitions.Add(new ColumnDefinition());
        labelRow.ColumnDefinitions.Add(new ColumnDefinition());
        var percentText = new TextBlock
        {
            Text = window is null ? $"{windowLabel} · —" : $"{windowLabel} · {window.UsedPercentage:0}%",
            FontSize = 10,
            Foreground = (Brush)FindResource("AppMutedForegroundBrush"),
        };
        Grid.SetColumn(percentText, 0);
        var resetText = new TextBlock
        {
            Text = FormatResetsAt(window?.ResetsAt, windowLabel),
            FontSize = 10,
            Foreground = (Brush)FindResource("AppMutedForegroundBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(resetText, 1);
        labelRow.Children.Add(percentText);
        labelRow.Children.Add(resetText);
        container.Children.Add(labelRow);

        return container;
    }

    private static string FormatResetsAt(DateTimeOffset? resetsAt, string windowLabel)
    {
        if (resetsAt is null)
        {
            return "";
        }
        var remaining = resetsAt.Value - DateTimeOffset.UtcNow;
        if (windowLabel == "5h")
        {
            return remaining <= TimeSpan.Zero
                ? "resetting..."
                : $"resets in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        }
        // ponytail: fixed to InvariantCulture — with the OS locale, "MMM d"
        // rendered as "авг. 29" on a Russian-locale Windows install.
        return $"resets {resetsAt.Value.LocalDateTime.ToString("MMM d", CultureInfo.InvariantCulture)}";
    }
}
