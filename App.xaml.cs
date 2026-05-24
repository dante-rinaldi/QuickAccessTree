using System.Windows;
using System.Windows.Interop;
using SidebarBuddy.Interop;
using SidebarBuddy.Models;
using SidebarBuddy.Services;
using Forms = System.Windows.Forms;
using ThemeManager = SidebarBuddy.Services.ThemeManager;

namespace SidebarBuddy;

public partial class App : System.Windows.Application
{
    private static Mutex?          _singleInstanceMutex;
    private MainWindow?            _mainWindow;
    private ExplorerAttachService? _attachService;
    private Forms.NotifyIcon?      _trayIcon;
    private SettingsService        _settingsService = new();
    private SettingsWindow?        _settingsWindow;
    public  AppSettings            Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "SidebarBuddy_SingleInstance_v1", out bool created);
        if (!created)
        {
            _singleInstanceMutex.Dispose();
            System.Windows.MessageBox.Show("Sidebar Buddy is already running. Check the system tray.",
                "Sidebar Buddy", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            ex.Handled = true;
            System.Windows.MessageBox.Show(
                $"Unexpected error:\n{ex.Exception.Message}\n\n{ex.Exception.GetType().Name}",
                "Sidebar Buddy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        base.OnStartup(e);
        Settings = _settingsService.Load();

        // Fix: IsRegistered=true with no key is an invalid state — treat as trial
        if (Settings.IsRegistered &&
            (string.IsNullOrEmpty(Settings.LicenseKey) || string.IsNullOrEmpty(Settings.RegisteredEmail)))
        {
            Settings.IsRegistered    = false;
            Settings.LicenseKey      = null;
            Settings.RegisteredEmail = null;
            Settings.LastValidated   = null;
            _settingsService.Save(Settings);
        }

        // For trial users: get authoritative trial start date from server (non-blocking best-effort).
        // This overwrites a locally manipulated TrialStartDate with the server's record for this device.
        if (!Settings.IsRegistered)
            _ = SyncTrialStartDateAsync();

        // Block expired trial before showing anything
        if (!Settings.IsRegistered && (DateTime.UtcNow - Settings.TrialStartDate).Days >= 15)
        {
            var expired = new TrialExpiredWindow(Settings, _settingsService);
            expired.ShowDialog();
            if (!Settings.IsRegistered)
            {
                Shutdown();
                return;
            }
        }

        ThemeManager.Apply(Settings.Theme);
        ThemeManager.ApplyFontScale(Settings.FontScale);

        _mainWindow = new MainWindow();

        // Show off-screen so WPF builds and renders the visual tree,
        // then hide immediately. This gives us a valid, content-ready HWND.
        _mainWindow.ShowActivated = false;
        _mainWindow.Show();

        // Apply WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW: the sidebar never
        // steals focus and never appears in Alt+Tab.
        var helper = new WindowInteropHelper(_mainWindow);
        int ex = NativeMethods.GetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE,
            ex | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);

        ThemeManager.WindowHandle = helper.Handle;
        ThemeManager.ApplySkin(Settings.Skin);
        ThemeManager.ApplyAppearance(Settings);

        _mainWindow.Hide(); // parked off-screen; ExplorerAttachService shows it

        _attachService = new ExplorerAttachService(
            _mainWindow, Settings.SidebarWidthDip, Settings.DockSide);
        _mainWindow.Initialize(Settings, _attachService, _settingsService);
        _mainWindow.UpdateTrialBanner();
        _attachService.OnReattached = _mainWindow.ClearTreeSelection;
        _attachService.Start();

        // Background license re-validation — non-blocking, never delays startup
        if (Settings.IsRegistered)
            _ = RevalidateLicenseInBackgroundAsync();

        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text    = "Sidebar Buddy",
            Visible = true,
            Icon    = System.Drawing.Icon.ExtractAssociatedIcon(
                          System.Reflection.Assembly.GetExecutingAssembly().Location)
                      ?? System.Drawing.SystemIcons.Application,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show sidebar", null, (_, _) => Dispatcher.Invoke(ForceShow));
        menu.Items.Add("Settings…",    null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Shutdown));

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick     += (_, _) => Dispatcher.Invoke(ForceShow);
    }

    public void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        try
        {
            _settingsWindow = new SettingsWindow(
                Settings,
                _settingsService,
                _attachService,
                onApplied: () =>
                {
                    _mainWindow?.ReloadTree();
                    _mainWindow?.ApplyQuickLinks();
                    _mainWindow?.ApplyDockCorners();
                    _mainWindow?.UpdateTrialBanner();
                    if (_attachService != null)
                        _attachService.AutoHide = Settings.AutoHide;
                });
            _settingsWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not open Settings:\n{ex.Message}\n\n{ex.GetType().Name}",
                "Sidebar Buddy", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SyncTrialStartDateAsync()
    {
        var result = await LicenseService.CheckTrialAsync();
        if (result == null) return; // server unreachable — keep local date

        // Server is authoritative: overwrite local trial start date
        if (result.Value.StartDate > Settings.TrialStartDate ||
            result.Value.StartDate < Settings.TrialStartDate.AddDays(-1))
        {
            Settings.TrialStartDate = result.Value.StartDate;
            _settingsService.Save(Settings);
        }

        // If server says expired but local check hasn't caught it yet, block now
        if (result.Value.DaysRemaining <= 0 && !Settings.IsRegistered)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var expired = new TrialExpiredWindow(Settings, _settingsService);
                expired.ShowDialog();
                if (!Settings.IsRegistered) Shutdown();
            });
        }
    }

    private async Task RevalidateLicenseInBackgroundAsync()
    {
        if (string.IsNullOrEmpty(Settings.LicenseKey) || string.IsNullOrEmpty(Settings.RegisteredEmail))
            return;

        bool? result = await LicenseService.RevalidateAsync(Settings.RegisteredEmail, Settings.LicenseKey);

        if (result == true)
        {
            Settings.LastValidated = DateTime.UtcNow;
            _settingsService.Save(Settings);
            return;
        }

        if (result == null) // server unreachable — apply 7-day grace period
        {
            double daysSinceLast = Settings.LastValidated.HasValue
                ? (DateTime.UtcNow - Settings.LastValidated.Value).TotalDays
                : 0; // never validated yet (first launch after activation) — full grace
            if (daysSinceLast <= 7) return;
        }

        // Key revoked/deleted, or grace period expired — deregister and block
        await Dispatcher.InvokeAsync(() =>
        {
            Settings.IsRegistered    = false;
            Settings.LicenseKey      = null;
            Settings.RegisteredEmail = null;
            Settings.LastValidated   = null;
            _settingsService.Save(Settings);
            _mainWindow?.UpdateTrialBanner();

            var expired = new TrialExpiredWindow(Settings, _settingsService);
            expired.ShowDialog();
            if (!Settings.IsRegistered) Shutdown();
        });
    }

    private void ForceShow()
    {
        // Try to attach to an open Explorer; if none, just reveal the sidebar
        if (_attachService != null)
            _attachService.ForceAttach();
        else if (_mainWindow is { IsVisible: false })
            _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _attachService?.Dispose();
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
