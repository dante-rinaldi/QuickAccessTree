using System.Windows;
using System.Windows.Input;
using SidebarBuddy.Models;
using SidebarBuddy.Services;

namespace SidebarBuddy;

public partial class TrialExpiredWindow : Window
{
    private readonly AppSettings    _settings;
    private readonly SettingsService _settingsSvc;

    public TrialExpiredWindow(AppSettings settings, SettingsService settingsSvc)
    {
        InitializeComponent();
        _settings    = settings;
        _settingsSvc = settingsSvc;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void BuyNow_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "https://sidebarbuddy.com/#pricing",
            UseShellExecute = true
        });
    }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        string email = EmailBox.Text.Trim();
        string key   = KeyBox.Text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(key))
        {
            ErrorText.Text       = "Please enter both your email address and license key.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ActivateBtn.IsEnabled = false;
        ActivateBtn.Content   = "Checking…";

        var (valid, error) = await LicenseService.ValidateLicenseAsync(email, key);

        ActivateBtn.IsEnabled = true;
        ActivateBtn.Content   = "Activate";

        if (valid)
        {
            _settings.IsRegistered    = true;
            _settings.LicenseKey      = key;
            _settings.RegisteredEmail = email;
            _settingsSvc.Save(_settings);

            FormPanel.Visibility    = Visibility.Collapsed;
            SuccessSubText.Text     = $"Sidebar Buddy is now registered to\n{email}";
            SuccessPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorText.Text       = error ?? "Activation failed. Please check your email and key.";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
