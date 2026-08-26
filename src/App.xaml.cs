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

    // True only when the tray's Exit was clicked, vs. closing the window (hides to tray).
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

        _trayIcon.ForceCreate(); // lives in Application.Resources, not a window — needs a manual create

        _ = RefreshAllUsageAsync();
        _ = RunUsageRefreshLoopAsync();
        _ = RunUpdateCheckLoopAsync();

        ShowMainWindow();
    }

    // Doubles as "check now" and "install what was found" to avoid two menu items.
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
            MessageWindow.ShowInfo(_mainWindow, "Claude Account Switcher", "You're on the latest version.");
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
        // Disable immediately so repeated clicks can't start concurrent downloads.
        _updateMenuItem.IsEnabled = false;
        _updateMenuItem.Header = "Downloading update...";
        _mainWindow?.SetBusy(true, "Downloading update...");
        try
        {
            var installerPath = Path.Combine(Path.GetTempPath(), $"ClaudeAccountSwitcherSetup-{update.TagName}.exe");
            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(update.InstallerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            // Launch detached and exit for real — the installer overwrites our own files.
            Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
            {
                UseShellExecute = true,
            });
            IsExiting = true;
            Shutdown();
        }
        catch (Exception ex)
        {
            _mainWindow?.SetBusy(false);
            SetAvailableUpdate(update); // restores the clickable "Update to vX..." state
            MessageWindow.ShowError(_mainWindow, "Update failed", $"Couldn't download or start the update: {ex.Message}");
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
            catch (Exception)
            {
                _usageCache[entry.Id] = null;
            }
        }

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
        using var warningBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 0x9C, 0x7A, 0x45)); // matches BarFillWarningBrush
        graphics.FillEllipse(warningBrush, innerRect);

        // Icon.FromHandle leaks the native HICON without a DestroyIcon call —
        // fine here, runs only a handful of times per session.
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
