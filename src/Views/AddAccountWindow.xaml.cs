using System.Windows;
using System.Windows.Threading;

namespace ClaudeAccountSwitcher;

public partial class AddAccountWindow : Window
{
    private readonly OAuthClient _oauthClient;
    private readonly ProfileClient _profileClient;
    private readonly AccountStore _accountStore;
    private readonly CancellationTokenSource _cts = new();

    public AddAccountWindow(ITokenEndpointClient tokenEndpoint, ProfileClient profileClient, AccountStore accountStore)
    {
        InitializeComponent();
        _oauthClient = new OAuthClient(tokenEndpoint);
        _profileClient = profileClient;
        _accountStore = accountStore;
        Closing += (_, _) => _cts.Cancel();
    }

    private async void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(WaitingPanel);
        try
        {
            var account = await _oauthClient.RunFlowAsync(_cts.Token);
            var profile = await _profileClient.GetProfileAsync(account.AccessToken, _cts.Token);

            string displayName;
            string? email;
            string? organizationUuid;
            bool isTeamAccount;

            if (profile is not null)
            {
                displayName = profile.DisplayName;
                email = profile.Email;
                organizationUuid = profile.OrganizationUuid;
                isTeamAccount = profile.IsTeamAccount;
            }
            else
            {
                // ponytail: profile lookup failed (network/unexpected shape) but
                // the OAuth token itself is valid — don't block adding the
                // account over a display-name nicety. Generate a numbered
                // placeholder; the user can rename it later from the tray menu.
                displayName = $"Account {_accountStore.ListAccounts().Count + 1}";
                email = null;
                organizationUuid = null;
                isTeamAccount = false;
            }

            var existing = organizationUuid is not null ? _accountStore.FindByOrganizationUuid(organizationUuid) : null;
            if (existing is not null)
            {
                // ponytail: this also naturally clears any "needs
                // re-authorization" tray state for this account — the next
                // background refresh tick (within 60s) will succeed against
                // this fresh token and remove it from the reauth set itself,
                // no extra cross-window wiring needed here.
                _accountStore.SaveAccount(existing.Id, account);
                ShowDone(existing.Label, existing.IsTeamAccount, "Tokens refreshed");
            }
            else
            {
                _accountStore.AddAccount(displayName, account, email, organizationUuid, isTeamAccount);
                ShowDone(displayName, isTeamAccount, "Added to your accounts");
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Couldn't sign in: {ex.Message}";
            ShowPanel(ErrorPanel);
        }
    }

    private void ShowDone(string name, bool isTeamAccount, string status)
    {
        DoneNameText.Text = name;
        DoneBadgeText.Text = isTeamAccount ? "TEAM" : "PERSONAL";
        DoneStatusText.Text = status;
        ShowPanel(DonePanel);

        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        closeTimer.Tick += (_, _) => { closeTimer.Stop(); Close(); };
        closeTimer.Start();
    }

    private void ShowPanel(FrameworkElement panel)
    {
        IdlePanel.Visibility = Visibility.Collapsed;
        WaitingPanel.Visibility = Visibility.Collapsed;
        DonePanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }
}
