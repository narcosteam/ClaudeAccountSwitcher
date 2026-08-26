using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;

namespace ClaudeAccountSwitcher;

public partial class App : Application
{
    private AccountStore _accountStore = null!;
    private SwitcherService _switcher = null!;
    private UsageClient _usageClient = null!;
    private ProfileClient _profileClient = null!;
    private ITokenEndpointClient _tokenEndpoint = null!;
    private UpdateChecker _updateChecker = null!;
    private TaskbarIcon _trayIcon = null!;
    private MenuItem _updateMenuItem = null!;
    private MainWindow? _mainWindow;
    private readonly HashSet<string> _accountsNeedingReauth = new();
    private readonly Dictionary<string, UsageInfo?> _usageCache = new();
    private UpdateInfo? _availableUpdate;

    // ponytail: distinguishes "user clicked X on the main window" (hide to
    // tray) from "tray Exit was clicked" (actually quit) — MainWindow checks
    // this in its Closing handler.
    internal bool IsExiting { get; private set; }

    internal AccountStore AccountStore => _accountStore;
    internal SwitcherService Switcher => _switcher;
    internal ITokenEndpointClient TokenEndpoint => _tokenEndpoint;
    internal ProfileClient ProfileClient => _profileClient;
    internal UsageInfo? GetCachedUsage(string accountId) => _usageCache.GetValueOrDefault(accountId);
    internal bool NeedsReauth(string accountId) => _accountsNeedingReauth.Contains(accountId);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeAccountSwitcher");
        var credentialsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", ".credentials.json");

        _accountStore = new AccountStore(appDataDir);
        _switcher = new SwitcherService(_accountStore, credentialsPath);

        var httpClient = new HttpClient();
        _tokenEndpoint = new TokenEndpointClient(httpClient);
        _usageClient = new UsageClient(httpClient, new TokenRefresher(_tokenEndpoint), _accountStore);
        _profileClient = new ProfileClient(httpClient);
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        _updateChecker = new UpdateChecker(httpClient, currentVersion);

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        UpdateTrayIcon();

        var menu = new ContextMenu();
        var restoreItem = new MenuItem { Header = "Restore" };
        restoreItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(restoreItem);
        _updateMenuItem = new MenuItem { Header = "Check for Updates" };
        _updateMenuItem.Click += (_, _) => _ = UpdateMenuItem_ClickAsync();
        menu.Items.Add(_updateMenuItem);
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => { IsExiting = true; Shutdown(); };
        menu.Items.Add(exitItem);
        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayLeftMouseDoubleClick += (_, _) => ShowMainWindow();

        _trayIcon.ForceCreate(); // icon lives in Application.Resources, not a window, so it needs a manual create

        _ = RefreshAllUsageAsync();
        _ = RunUsageRefreshLoopAsync();
        _ = RunUpdateCheckLoopAsync();

        // ponytail: every launch shows the window (fresh install, post-update
        // relaunch, or a normal run) instead of landing silently in the tray
        // — there's no autostart-at-login path today where a popup would be
        // unwelcome; closing it still just hides to tray as usual.
        ShowMainWindow();
    }

    // ponytail: one menu item does double duty — "check now" when nothing's
    // pending, "install what I already found" once a check has succeeded.
    // Two buttons for one action just makes the menu longer.
    private async Task UpdateMenuItem_ClickAsync()
    {
        if (_availableUpdate is { } update)
        {
            await ApplyUpdateAsync(update);
            return;
        }

        _updateMenuItem.IsEnabled = false;
        _updateMenuItem.Header = "Checking...";
        var found = await _updateChecker.CheckForUpdateAsync(CancellationToken.None);
        SetAvailableUpdate(found);
        if (found is null)
        {
            MessageBox.Show("You're on the latest version.", "Claude Account Switcher", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task RunUpdateCheckLoopAsync()
    {
        while (true)
        {
            SetAvailableUpdate(await _updateChecker.CheckForUpdateAsync(CancellationToken.None));
            await Task.Delay(TimeSpan.FromDays(1));
        }
    }

    private void SetAvailableUpdate(UpdateInfo? update)
    {
        _availableUpdate = update;
        _updateMenuItem.IsEnabled = true;
        _updateMenuItem.Header = update is null ? "Check for Updates" : $"Update to v{update.TagName}...";
        UpdateTrayIcon();
    }

    private async Task ApplyUpdateAsync(UpdateInfo update)
    {
        // ponytail: disable immediately — the download can take a while and
        // without this the menu item was still clickable, spawning another
        // concurrent download/install per click.
        _updateMenuItem.IsEnabled = false;
        _updateMenuItem.Header = "Downloading update...";
        try
        {
            var installerPath = Path.Combine(Path.GetTempPath(), $"ClaudeAccountSwitcherSetup-{update.TagName}.exe");
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(update.InstallerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            // ponytail: installer overwrites files this running process has
            // open, so it must launch detached and outlive us — spawn it,
            // then exit for real (not hide-to-tray) so the file locks clear.
            Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
            {
                UseShellExecute = true,
            });
            IsExiting = true;
            Shutdown();
        }
        catch (Exception ex) // ponytail: download/launch failure — don't lose the update state, just let the user retry from the tray menu
        {
            SetAvailableUpdate(update); // restores the clickable "Update to vX..." state
            MessageBox.Show($"Couldn't download or start the update: {ex.Message}", "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowMainWindow()
    {
        _mainWindow ??= new MainWindow(this);
        _mainWindow.RefreshAccounts();
        _mainWindow.Show();
        _mainWindow.Activate();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
    }

    private async Task RunUsageRefreshLoopAsync()
    {
        while (true)
        {
            var delay = RefreshScheduler.DelayUntilNextMinuteBoundary(DateTimeOffset.UtcNow);
            await Task.Delay(delay);
            await RefreshAllUsageAsync();
        }
    }

    private async Task RefreshAllUsageAsync()
    {
        foreach (var entry in _accountStore.ListAccounts())
        {
            try
            {
                _usageCache[entry.Id] = await _usageClient.GetUsageAsync(entry.Id, CancellationToken.None);
                if (_accountsNeedingReauth.Remove(entry.Id))
                {
                    UpdateTrayIcon();
                }
            }
            catch (RefreshTokenRevokedException)
            {
                _usageCache[entry.Id] = null;
                if (_accountsNeedingReauth.Add(entry.Id))
                {
                    UpdateTrayIcon();
                }
            }
            catch (Exception) // ponytail: transient network/parse failure — leave usage unavailable, don't mark as needing reauth
            {
                _usageCache[entry.Id] = null;
            }
        }

        // ponytail: window is a singleton kept alive via Hide()/Show(), so a
        // simple "if visible, repaint" after each tick is enough to keep it
        // live — no pub/sub event plumbing needed for one subscriber.
        if (_mainWindow?.IsVisible == true)
        {
            _mainWindow.RefreshAccounts();
        }
    }

    private const int TrayIconSize = 64;

    private void UpdateTrayIcon()
    {
        _trayIcon.Icon = BuildTrayIconImage(_accountsNeedingReauth.Count > 0 || _availableUpdate is not null);
    }

    private static Bitmap LoadBaseIconBitmap()
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream("TrayIcon.png")
            ?? throw new InvalidOperationException("Embedded TrayIcon.png resource not found.");
        using var source = new Bitmap(stream);
        var scaled = new Bitmap(TrayIconSize, TrayIconSize);
        using var graphics = Graphics.FromImage(scaled);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, TrayIconSize, TrayIconSize);
        return scaled;
    }

    private static Icon BuildTrayIconImage(bool needsAttention)
    {
        using var bitmap = LoadBaseIconBitmap();
        if (!needsAttention)
        {
            return Icon.FromHandle(bitmap.GetHicon());
        }

        using var graphics = Graphics.FromImage(bitmap);
        var dotSize = bitmap.Width / 3;
        var dotRect = new Rectangle(bitmap.Width - dotSize, bitmap.Height - dotSize, dotSize, dotSize);
        graphics.FillEllipse(System.Drawing.Brushes.White, dotRect);
        var innerRect = Rectangle.Inflate(dotRect, -dotSize / 8, -dotSize / 8);
        using var warningBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 0x9C, 0x7A, 0x45)); // matches BarFillWarningBrush — quiet, not alarming
        graphics.FillEllipse(warningBrush, innerRect);

        // ponytail: Icon.FromHandle wraps a native HICON that technically
        // leaks if never destroyed via the Win32 DestroyIcon call —
        // acceptable here since this only runs when the reauth set flips
        // between empty and non-empty, at most a handful of times per
        // session. Add a DestroyIcon P/Invoke wrapper if this ever runs on a
        // hot path.
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
