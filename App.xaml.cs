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

        base.OnStartup(e);
        Settings = _settingsService.Load();
        ThemeManager.Apply(Settings.Theme);

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

        _mainWindow.Hide(); // parked off-screen; ExplorerAttachService shows it

        _attachService = new ExplorerAttachService(
            _mainWindow, Settings.SidebarWidthDip, Settings.DockSide);
        _mainWindow.Initialize(Settings, _attachService, _settingsService);
        _attachService.Start();

        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text    = "Sidebar Buddy",
            Visible = true,
            Icon    = System.Drawing.SystemIcons.Application,
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
        if (_settingsWindow != null && _settingsWindow.IsLoaded)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(
            Settings,
            _settingsService,
            _attachService,
            onApplied: () =>
            {
                _mainWindow?.ReloadTree();
                _mainWindow?.ApplyQuickLinks();
                if (_attachService != null)
                    _attachService.AutoHide = Settings.AutoHide;
            });
        _settingsWindow.Show();
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
