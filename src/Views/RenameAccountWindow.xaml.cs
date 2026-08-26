using System.Windows;

namespace ClaudeAccountSwitcher;

public partial class RenameAccountWindow : Window
{
    private readonly AccountStore _accountStore;
    private readonly string _accountId;

    public RenameAccountWindow(AccountStore accountStore, string accountId, string currentLabel)
    {
        InitializeComponent();
        _accountStore = accountStore;
        _accountId = accountId;
        NameBox.Text = currentLabel;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var newLabel = NameBox.Text.Trim();
        if (!string.IsNullOrEmpty(newLabel))
        {
            _accountStore.RenameAccount(_accountId, newLabel);
        }
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
