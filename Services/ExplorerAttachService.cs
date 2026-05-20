using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using SidebarBuddy.Interop;
using SidebarBuddy.Models;

namespace SidebarBuddy.Services;

public class ExplorerAttachService : IDisposable
{
    private readonly Window _sidebar;
    private double   _sidebarWidthDip;
    private DockSide _dockSide;

    private NativeMethods.WinEventProc? _foregroundProc;
    private NativeMethods.WinEventProc? _locationProc;
    private nint _foregroundHook;
    private nint _locationHook;

    private nint _explorerHwnd;
    private System.Timers.Timer?        _pollTimer;
    private CancellationTokenSource?    _showCts;

    public double ShowDelaySecs { get; set; } = 0;
    public bool   AutoHide      { get; set; } = false;
    public bool   IsCollapsed   { get; set; } = false;

    private const double CollapsedWidthDip = 20.0;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc fn, nint lp);
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    public ExplorerAttachService(Window sidebar, double widthDip, DockSide dockSide)
    {
        _sidebar         = sidebar;
        _sidebarWidthDip = widthDip;
        _dockSide        = dockSide;
    }

    public void Start()
    {
        _foregroundProc = OnForeground;
        _locationProc   = OnLocation;

        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            nint.Zero, _foregroundProc, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _locationHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            nint.Zero, _locationProc, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _pollTimer = new System.Timers.Timer(500) { AutoReset = true };
        _pollTimer.Elapsed += OnPollElapsed;
        _pollTimer.Start();
    }

    public void UpdateDockSide(DockSide side)
    {
        _dockSide = side;
        if (_explorerHwnd != nint.Zero) Dispatch(() => SnapSidebar(_explorerHwnd));
    }

    public void RefreshPosition()
    {
        if (_explorerHwnd != nint.Zero) Dispatch(() => SnapSidebar(_explorerHwnd));
    }

    public void UpdateWidth(double dipWidth)
    {
        _sidebarWidthDip = dipWidth;
        if (_explorerHwnd != nint.Zero) Dispatch(() => SnapSidebar(_explorerHwnd));
    }

    // ── Poll ─────────────────────────────────────────────────────────────

    private void OnPollElapsed(object? sender, ElapsedEventArgs e)
        => Dispatch(PollOnUI);

    private void PollOnUI()
    {
        nint fg = NativeMethods.GetForegroundWindow();
        if (fg == nint.Zero) return;

        if (IsExplorerWindow(fg))
        {
            if (fg != _explorerHwnd)
                Track(fg);
            else if (!SidebarIsVisible())
                ShowSidebar(immediate: false);
        }
        else
        {
            NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
            if ((int)pid != Environment.ProcessId && SidebarIsVisible()
                && AutoHide && !IsCollapsed)
                HideSidebar();
        }
    }

    // ── WinEvent callbacks ────────────────────────────────────────────────

    private void OnForeground(nint hook, uint evt, nint hwnd,
        int idObj, int idChild, uint thread, uint time)
    {
        if (hwnd == nint.Zero) return;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if ((int)pid == Environment.ProcessId) return;

        if (IsExplorerWindow(hwnd))
            Dispatch(() => Track(hwnd));
        else if (AutoHide && !IsCollapsed)
            Dispatch(HideSidebar);
    }

    private void OnLocation(nint hook, uint evt, nint hwnd,
        int idObj, int idChild, uint thread, uint time)
    {
        if (hwnd != _explorerHwnd) return;
        if (!NativeMethods.IsWindow(hwnd))
        {
            Dispatch(() => { _explorerHwnd = nint.Zero; HideSidebar(); });
            return;
        }
        Dispatch(() => SnapSidebar(hwnd));
    }

    // ── Track / detach ────────────────────────────────────────────────────

    private void Track(nint hwnd)
    {
        _explorerHwnd = hwnd;
        SnapSidebar(hwnd);
        ShowSidebar(immediate: false);
    }

    public void ExplicitDetach()
    {
        _explorerHwnd = nint.Zero;
        HideSidebar();
    }

    public void ForceAttach()
    {
        if (_explorerHwnd != nint.Zero && NativeMethods.IsWindow(_explorerHwnd))
        { SnapSidebar(_explorerHwnd); ShowSidebar(immediate: true); return; }

        nint found = nint.Zero;
        EnumWindows((h, _) =>
        {
            if (IsExplorerWindow(h) && NativeMethods.IsWindowVisible(h))
            { found = h; return false; }
            return true;
        }, nint.Zero);

        if (found != nint.Zero) { _explorerHwnd = found; SnapSidebar(found); ShowSidebar(immediate: true); }
    }

    // ── Positioning ───────────────────────────────────────────────────────

    private void SnapSidebar(nint explorerHwnd)
    {
        if (!NativeMethods.GetWindowRect(explorerHwnd, out var ex)) return;
        double dpi = DpiScale(explorerHwnd);
        double w = IsCollapsed ? CollapsedWidthDip : _sidebarWidthDip;

        double x = _dockSide == DockSide.Right
            ? ex.Right / dpi
            : ex.Left  / dpi - w;

        _sidebar.Left   = x;
        _sidebar.Top    = ex.Top    / dpi;
        _sidebar.Width  = w;
        _sidebar.Height = ex.Height / dpi;
    }

    // ── Show / Hide ───────────────────────────────────────────────────────

    private void ShowSidebar(bool immediate)
    {
        if (!immediate && ShowDelaySecs > 0)
        {
            _showCts?.Cancel();
            _showCts?.Dispose();
            _showCts = new CancellationTokenSource();
            var token = _showCts.Token;
            _ = Task.Delay(TimeSpan.FromSeconds(ShowDelaySecs), token)
                    .ContinueWith(_ => Dispatch(ShowSidebarNow),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.Default);
            return;
        }
        ShowSidebarNow();
    }

    private void ShowSidebarNow()
    {
        // WPF must be Visible for it to render into the HWND. A bare Win32
        // ShowWindow won't bring rendering back after Window.Hide().
        _sidebar.Visibility = Visibility.Visible;

        var helper = new WindowInteropHelper(_sidebar);
        nint hwnd = helper.Handle;
        if (hwnd == nint.Zero) return;

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void HideSidebar()
    {
        _showCts?.Cancel();
        var helper = new WindowInteropHelper(_sidebar);
        NativeMethods.SetWindowPos(helper.Handle, NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_HIDEWINDOW);
        _sidebar.Visibility = Visibility.Hidden;
    }

    private bool SidebarIsVisible()
    {
        var helper = new WindowInteropHelper(_sidebar);
        return helper.Handle != nint.Zero && NativeMethods.IsWindowVisible(helper.Handle);
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private static readonly Guid ShellWindowsCLSID =
        new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");

    public void NavigateTo(string path)
    {
        try
        {
            Type? t = Type.GetTypeFromCLSID(ShellWindowsCLSID);
            if (t == null) return;
            dynamic sw    = Activator.CreateInstance(t)!;
            int     count = (int)sw.Count;

            dynamic? target = null;
            for (int i = 0; i < count; i++)
            {
                dynamic? w = sw.Item(i);
                if (w == null) continue;
                try { if ((nint)(int)w.HWND == _explorerHwnd) { target = w; break; } }
                catch { }
            }
            if (target == null && count > 0)
                try { target = sw.Item(0); } catch { }

            if (target == null) return;
            object pv = path;
            target.Navigate2(ref pv);
        }
        catch { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void Dispatch(Action action)
        => Application.Current?.Dispatcher.BeginInvoke(action);

    public static bool IsExplorerWindow(nint hwnd)
    {
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString() is "CabinetWClass" or "ExploreWClass";
    }

    private static double DpiScale(nint hwnd)
    {
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi > 0 ? dpi / 96.0 : 1.0;
    }

    public void Dispose()
    {
        _showCts?.Cancel();
        _showCts?.Dispose();
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        if (_foregroundHook != nint.Zero) NativeMethods.UnhookWinEvent(_foregroundHook);
        if (_locationHook   != nint.Zero) NativeMethods.UnhookWinEvent(_locationHook);
    }
}
