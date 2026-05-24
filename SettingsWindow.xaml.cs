using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SidebarBuddy.Models;
using SidebarBuddy.Services;

namespace SidebarBuddy;

public partial class SettingsWindow : Window
{
    private readonly AppSettings            _settings;
    private readonly SettingsService        _settingsSvc;
    private readonly ExplorerAttachService? _attachSvc;
    private readonly Action?                _onApplied;
    private bool _loading;

    private static readonly string[] HighlightPresets =
    {
        // Bright
        "#FFC000", "#FF5252", "#69F0AE", "#40C4FF", "#FF9100", "#E040FB",
        "#00E5FF", "#FF4081", "#BDBDBD", "#90A4AE", "#BCAAA4", "#FFFFFF",
        // Dark
        "#B45309", "#991B1B", "#166534", "#1E40AF", "#9A3412", "#6D28D9",
        "#0E7490", "#9D174D", "#4B5563", "#1E3A5F", "#57534E", "#374151",
    };

    public SettingsWindow(
        AppSettings            settings,
        SettingsService        settingsSvc,
        ExplorerAttachService? attachSvc,
        Action?                onApplied)
    {
        _settings    = settings;
        _settingsSvc = settingsSvc;
        _attachSvc   = attachSvc;
        _onApplied   = onApplied;

        _loading = true;
        InitializeComponent();
        PopulateValues();
        NavList.SelectedIndex = 0;
    }

    // ── Populate ──────────────────────────────────────────────────────

    private void PopulateValues()
    {
        _loading = true;

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
        CbStartup.IsChecked         = _settingsSvc.GetLaunchOnStartup();
        CbRestoreExpanded.IsChecked = _settings.RestoreExpandedState;

        // General — add folder mode
        RbAddCurrent.IsChecked  = _settings.AddFolderBehavior == AddFolderMode.CurrentFolder;
        RbAddSelected.IsChecked = _settings.AddFolderBehavior == AddFolderMode.SelectedItem;
        RbAddBrowse.IsChecked   = _settings.AddFolderBehavior == AddFolderMode.BrowseDialog;

        // Quick links
        CbShowThisPC.IsChecked       = _settings.ShowThisPC;
        CbShowControlPanel.IsChecked = _settings.ShowControlPanel;
        RbQuickLinksTop.IsChecked    = _settings.QuickLinksPosition == QuickLinkPosition.Top;
        RbQuickLinksBottom.IsChecked = _settings.QuickLinksPosition == QuickLinkPosition.Bottom;

        // Appearance — theme
        RbThemeSystem.IsChecked = _settings.Theme == ThemeMode.System;
        RbThemeDark.IsChecked   = _settings.Theme == ThemeMode.Dark;
        RbThemeLight.IsChecked  = _settings.Theme == ThemeMode.Light;

        // Appearance — skin (matched by Tag, falls back to first item)
        string skinName = _settings.Skin.ToString();
        SkinList.SelectedItem = SkinList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == skinName)
            ?? SkinList.Items[0];

        // Appearance — font scale
        FontScaleSlider.Value = _settings.FontScale;
        UpdateFontScaleLabel(_settings.FontScale);

        // License
        RefreshLicensePanel();

        // Customization
        OpacitySlider.Value = _settings.SidebarOpacity;
        UpdateOpacityLabel(_settings.SidebarOpacity);
        BuildHighlightSwatches();
        CbTextGlow.IsChecked        = _settings.TextGlow;
        GlowSlider.Value            = _settings.TextGlowIntensity;
        UpdateGlowLabel(_settings.TextGlowIntensity);
        GlowIntensityRow.Visibility = _settings.TextGlow ? Visibility.Visible : Visibility.Collapsed;
        // Background image
        CbShowBgImage.IsChecked = _settings.ShowBackgroundImage;
        BgOpacitySlider.Value   = _settings.BackgroundImageOpacity;
        UpdateBgOpacityLabel(_settings.BackgroundImageOpacity);
        UpdateCustomUploadPanelState(_settings.Skin);
        UpdateCustomImagePathBox();

        _loading = false;
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

    // ── Live-apply ────────────────────────────────────────────────────

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        LiveApply();
    }

    private void Skin_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (SkinList.SelectedItem is not ListBoxItem { Tag: string tag }) return;
        if (!Enum.TryParse<AppSkin>(tag, out var skin)) return;
        UpdateCustomUploadPanelState(skin);
        ApplySkinDefaults(skin);
        LiveApply();
    }

    private void ApplySkinDefaults(AppSkin skin)
    {
        var d = ThemeManager.GetSkinDefault(skin);
        if (d is null) return;

        var (bgOpacity, glow, glowIntensity) = d.Value;

        _loading = true;
        try
        {
            CbShowBgImage.IsChecked     = true;
            BgOpacitySlider.Value       = bgOpacity;
            UpdateBgOpacityLabel(bgOpacity);
            CbTextGlow.IsChecked        = glow;
            GlowSlider.Value            = glowIntensity;
            UpdateGlowLabel(glowIntensity);
            GlowIntensityRow.Visibility = glow ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _loading = false;
        }

        _settings.ShowBackgroundImage    = true;
        _settings.BackgroundImageOpacity = bgOpacity;
        _settings.TextGlow               = glow;
        _settings.TextGlowIntensity      = glowIntensity;
    }

    private void ResetToSkinDefaults_Click(object sender, RoutedEventArgs e)
    {
        ApplySkinDefaults(_settings.Skin);
        if (!_loading) LiveApply();
    }

    private void FontScaleSlider_ValueChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        double scale = Math.Round(e.NewValue, 2);
        UpdateFontScaleLabel(scale);
        _settings.FontScale = scale;
        ThemeManager.ApplyFontScale(scale);
        _settingsSvc.Save(_settings);
    }

    private void UpdateFontScaleLabel(double scale)
    {
        if (FontScaleLabel == null) return;
        FontScaleLabel.Text = $"{(int)Math.Round(scale * 100)}%";
    }

    private void LiveApply()
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

        // Add folder mode
        _settings.AddFolderBehavior = RbAddBrowse.IsChecked    == true ? AddFolderMode.BrowseDialog
                                    : RbAddSelected.IsChecked  == true ? AddFolderMode.SelectedItem
                                    : AddFolderMode.CurrentFolder;

        // Quick links
        _settings.ShowThisPC        = CbShowThisPC.IsChecked == true;
        _settings.ShowControlPanel  = CbShowControlPanel.IsChecked == true;
        _settings.QuickLinksPosition = RbQuickLinksTop.IsChecked == true
            ? QuickLinkPosition.Top : QuickLinkPosition.Bottom;

        // Appearance — theme
        ThemeMode newTheme = RbThemeLight.IsChecked == true ? ThemeMode.Light
                           : RbThemeDark.IsChecked  == true ? ThemeMode.Dark
                           : ThemeMode.System;
        _settings.Theme = newTheme;
        ThemeManager.Apply(newTheme);

        // Appearance — skin
        if (SkinList.SelectedItem is ListBoxItem { Tag: string skinTag } &&
            Enum.TryParse<AppSkin>(skinTag, out var selectedSkin))
            _settings.Skin = selectedSkin;
        ThemeManager.ApplySkin(_settings.Skin);

        // Live-apply service changes
        if (_attachSvc != null)
        {
            _attachSvc.UpdateDockSide(_settings.DockSide);
            _attachSvc.ShowDelaySecs = DelayToSeconds(_settings.VisibilityDelay);
            _attachSvc.AutoHide      = _settings.AutoHide;
        }

        // Customization
        _settings.SidebarOpacity         = Math.Round(OpacitySlider.Value, 2);
        _settings.TextGlow               = CbTextGlow.IsChecked == true;
        _settings.TextGlowIntensity      = Math.Round(GlowSlider.Value, 2);
        _settings.ShowBackgroundImage    = CbShowBgImage.IsChecked == true;
        _settings.BackgroundImageOpacity = Math.Round(BgOpacitySlider.Value, 2);
        ThemeManager.ApplyAppearance(_settings);

        _settingsSvc.Save(_settings);
        _onApplied?.Invoke();
    }

    // ── Customization helpers ─────────────────────────────────────────

    private void UpdateOpacityLabel(double v)
    {
        if (OpacityLabel == null) return;
        OpacityLabel.Text = $"{(int)Math.Round(v * 100)}%";
    }

    private void UpdateGlowLabel(double v)
    {
        if (GlowLabel == null) return;
        GlowLabel.Text = $"{(int)Math.Round(v * 100)}%";
    }

    private void UpdateBgOpacityLabel(double v)
    {
        if (BgOpacityLabel == null) return;
        BgOpacityLabel.Text = $"{(int)Math.Round(v * 100)}%";
    }

    private void BuildHighlightSwatches()
    {
        HighlightSwatchPanel.Children.Clear();
        foreach (var hex in HighlightPresets)
        {
            var s = new Border
            {
                Width        = 20,
                Height       = 20,
                CornerRadius = new CornerRadius(3),
                Margin       = new Thickness(0, 0, 4, 4),
                Background   = new System.Windows.Media.SolidColorBrush(
                                   (System.Windows.Media.Color)
                                   System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                Cursor       = Cursors.Hand,
                Tag          = hex,
                ToolTip      = hex,
            };
            // Highlight the active selection
            if (hex == _settings.HighlightColor)
                s.BorderBrush = System.Windows.Media.Brushes.White;
            s.BorderThickness = hex == _settings.HighlightColor
                ? new Thickness(2) : new Thickness(0);
            s.MouseLeftButtonUp += HighlightSwatch_Click;
            HighlightSwatchPanel.Children.Add(s);
        }
    }

    private void HighlightSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string hex }) return;
        _settings.HighlightColor = hex;
        BuildHighlightSwatches(); // refresh selection ring
        if (!_loading) ApplyAppearanceOnly();
    }

    private void ClearHighlight_Click(object sender, RoutedEventArgs e)
    {
        _settings.HighlightColor = null;
        BuildHighlightSwatches();
        if (!_loading) ApplyAppearanceOnly();
    }

    private void ApplyAppearanceOnly()
    {
        ThemeManager.ApplyAppearance(_settings);
        _settingsSvc.Save(_settings);
    }

    private void OpacitySlider_ValueChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        UpdateOpacityLabel(e.NewValue);
        LiveApply();
    }

    private void CbTextGlow_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        GlowIntensityRow.Visibility = CbTextGlow.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        LiveApply();
    }

    private void GlowSlider_ValueChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        UpdateGlowLabel(e.NewValue);
        LiveApply();
    }

    private void UpdateCustomUploadPanelState(AppSkin skin)
    {
        bool isCustom = skin == AppSkin.Custom;
        CustomUploadPanel.IsEnabled = isCustom;
        CustomUploadPanel.Opacity   = isCustom ? 1.0 : 0.38;
    }

    private void UpdateCustomImagePathBox()
    {
        if (CustomImagePathBox == null) return;
        CustomImagePathBox.Text = string.IsNullOrEmpty(_settings.CustomImagePath)
            ? "(none)" : _settings.CustomImagePath;
    }

    private void BrowseCustomImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select custom background image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        _settings.CustomImagePath = dlg.FileName;
        CustomImagePathBox.Text   = dlg.FileName;
        // Auto-switch to Custom skin
        var customItem = SkinList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == nameof(AppSkin.Custom));
        if (customItem != null && SkinList.SelectedItem != customItem)
            SkinList.SelectedItem = customItem; // fires Skin_Changed → LiveApply
        else
            LiveApply();
    }

    private void ClearCustomImage_Click(object sender, RoutedEventArgs e)
    {
        _settings.CustomImagePath = null;
        CustomImagePathBox.Text   = "(none)";
        LiveApply();
    }

    private void BgOpacitySlider_ValueChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        UpdateBgOpacityLabel(e.NewValue);
        LiveApply();
    }

    private static double DelayToSeconds(ShowDelay d) => d switch
    {
        ShowDelay.HalfSecond  => 0.5,
        ShowDelay.TwoSeconds  => 2.0,
        ShowDelay.FiveSeconds => 5.0,
        _                     => 0.0,
    };

    // ── Commands ──────────────────────────────────────────────────────

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── License ───────────────────────────────────────────────────────

    private void BuyNow_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://sidebarbuddy.com/#pricing");

    private void MyAccount_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://sidebarbuddy.com/account");

    private void Support_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://sidebarbuddy.com/support");

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = url,
            UseShellExecute = true
        });
    }

    private async void ActivateLicense_Click(object sender, RoutedEventArgs e)
    {
        LicenseErrorText.Visibility = Visibility.Collapsed;

        string email = LicenseEmailBox.Text.Trim();
        string key   = LicenseKeyBox.Text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(key))
        {
            LicenseErrorText.Text       = "Please enter both your email address and license key.";
            LicenseErrorText.Visibility = Visibility.Visible;
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
            LicenseEmailBox.Text = string.Empty;
            LicenseKeyBox.Text   = string.Empty;
            RefreshLicensePanel();
        }
        else
        {
            LicenseErrorText.Text       = error ?? "Activation failed. Please check your email and key.";
            LicenseErrorText.Visibility = Visibility.Visible;
        }
    }
}
