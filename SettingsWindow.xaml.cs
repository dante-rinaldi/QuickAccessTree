using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SidebarBuddy.Models;
using SidebarBuddy.Services;

namespace SidebarBuddy;

public partial class SettingsWindow : Window
{
    private readonly AppSettings           _settings;
    private readonly SettingsService       _settingsSvc;
    private readonly ExplorerAttachService? _attachSvc;
    private readonly Action?               _onApplied;

    public SettingsWindow(
        AppSettings           settings,
        SettingsService       settingsSvc,
        ExplorerAttachService? attachSvc,
        Action?               onApplied)
    {
        _settings    = settings;
        _settingsSvc = settingsSvc;
        _attachSvc   = attachSvc;
        _onApplied   = onApplied;

        InitializeComponent();
        PopulateValues();
        NavList.SelectedIndex = 0;
    }

    // ── Populate ──────────────────────────────────────────────────────

    private void PopulateValues()
    {
        // General — color inheritance
        RbColorPerFolder.IsChecked = _settings.ColorInheritance == ColorInheritanceMode.PerFolder;
        RbColorCascade.IsChecked   = _settings.ColorInheritance == ColorInheritanceMode.Cascade;

        // General — sidebar behavior
        RbStayVisible.IsChecked = !_settings.AutoHide;
        RbAutoHide.IsChecked    =  _settings.AutoHide;

        // General — delay
        RbDelayInstant.IsChecked = _settings.VisibilityDelay == ShowDelay.Instant;
        RbDelayHalf.IsChecked    = _settings.VisibilityDelay == ShowDelay.HalfSecond;
        RbDelayTwo.IsChecked     = _settings.VisibilityDelay == ShowDelay.TwoSeconds;
        RbDelayFive.IsChecked    = _settings.VisibilityDelay == ShowDelay.FiveSeconds;

        // General — dock side
        RbDockLeft.IsChecked  = _settings.DockSide == DockSide.Left;
        RbDockRight.IsChecked = _settings.DockSide == DockSide.Right;

        // General — behavior
        CbStartup.IsChecked        = _settingsSvc.GetLaunchOnStartup();
        CbRestoreExpanded.IsChecked = _settings.RestoreExpandedState;

        // Appearance — theme
        RbThemeSystem.IsChecked = _settings.Theme == ThemeMode.System;
        RbThemeDark.IsChecked   = _settings.Theme == ThemeMode.Dark;
        RbThemeLight.IsChecked  = _settings.Theme == ThemeMode.Light;

        // Appearance — skin
        int skinIdx = (int)_settings.Skin;
        if (skinIdx >= 0 && skinIdx < SkinList.Items.Count)
            SkinList.SelectedIndex = skinIdx;

        // License
        RefreshLicensePanel();
    }

    private void RefreshLicensePanel()
    {
        if (_settings.IsRegistered)
        {
            LicenseStatusText.Text       = "✓  Registered — Thank you for your purchase!";
            LicenseStatusText.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
            BuyNowBtn.Visibility         = Visibility.Collapsed;
        }
        else
        {
            int days = Math.Max(0, 15 - (DateTime.UtcNow - _settings.TrialStartDate).Days);
            LicenseStatusText.Text = days > 0
                ? $"Trial mode — {days} day{(days == 1 ? "" : "s")} remaining out of 15"
                : "Trial period has expired. Please purchase a license to continue.";

            LicenseStatusText.Foreground = days > 5
                ? System.Windows.Media.Brushes.Goldenrod
                : System.Windows.Media.Brushes.Tomato;

            BuyNowBtn.Visibility = Visibility.Visible;
        }
    }

    // ── Nav ───────────────────────────────────────────────────────────

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelGeneral == null) return;
        var tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string;
        PanelGeneral.Visibility    = tag == "General"    ? Visibility.Visible : Visibility.Collapsed;
        PanelAppearance.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        PanelLicense.Visibility    = tag == "License"    ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Commands ──────────────────────────────────────────────────────

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)  => Close();
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings();
        Close();
    }

    private void ApplySettings()
    {
        // Color inheritance
        _settings.ColorInheritance = RbColorCascade.IsChecked == true
            ? ColorInheritanceMode.Cascade : ColorInheritanceMode.PerFolder;

        // Sidebar behavior
        _settings.AutoHide = RbAutoHide.IsChecked == true;

        // Visibility delay
        _settings.VisibilityDelay = RbDelayHalf.IsChecked  == true ? ShowDelay.HalfSecond
                                  : RbDelayTwo.IsChecked   == true ? ShowDelay.TwoSeconds
                                  : RbDelayFive.IsChecked  == true ? ShowDelay.FiveSeconds
                                  : ShowDelay.Instant;

        // Dock side
        _settings.DockSide = RbDockLeft.IsChecked == true ? DockSide.Left : DockSide.Right;

        // Behavior
        _settings.RestoreExpandedState = CbRestoreExpanded.IsChecked == true;
        bool startup = CbStartup.IsChecked == true;
        _settings.LaunchOnStartup = startup;
        _settingsSvc.SetLaunchOnStartup(startup);

        // Appearance
        _settings.Theme = RbThemeLight.IsChecked  == true ? ThemeMode.Light
                        : RbThemeDark.IsChecked   == true ? ThemeMode.Dark
                        : ThemeMode.System;

        if (SkinList.SelectedIndex >= 0)
            _settings.Skin = (AppSkin)SkinList.SelectedIndex;

        // Live-apply service changes
        if (_attachSvc != null)
        {
            _attachSvc.UpdateDockSide(_settings.DockSide);
            _attachSvc.ShowDelaySecs = DelayToSeconds(_settings.VisibilityDelay);
            _attachSvc.AutoHide      = _settings.AutoHide;
        }

        _settingsSvc.Save(_settings);
        _onApplied?.Invoke();
    }

    private static double DelayToSeconds(ShowDelay d) => d switch
    {
        ShowDelay.HalfSecond  => 0.5,
        ShowDelay.TwoSeconds  => 2.0,
        ShowDelay.FiveSeconds => 5.0,
        _                     => 0.0,
    };

    // ── License ───────────────────────────────────────────────────────

    private void BuyNow_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "https://example.com/buy",   // placeholder — replace with real URL
            UseShellExecute = true
        });
    }

    private void ActivateLicense_Click(object sender, RoutedEventArgs e)
    {
        LicenseErrorText.Visibility = Visibility.Collapsed;
        string key = LicenseKeyBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(key)) return;

        if (IsValidLicenseKey(key))
        {
            _settings.IsRegistered = true;
            _settings.LicenseKey   = key;
            _settingsSvc.Save(_settings);
            LicenseKeyBox.Text = string.Empty;
            RefreshLicensePanel();
        }
        else
        {
            LicenseErrorText.Text       = "Invalid license key. Please check and try again.";
            LicenseErrorText.Visibility = Visibility.Visible;
        }
    }

    // Placeholder — replace with real HMAC / server validation
    private static bool IsValidLicenseKey(string key)
        => key.Length >= 16 && key.Contains('-');
}
