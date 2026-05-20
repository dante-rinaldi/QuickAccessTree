using System.Runtime.InteropServices;
using System.Text;

namespace QuickAccessTree.Interop;

internal static class NativeMethods
{
    // WinEvent constants
    public const uint EVENT_SYSTEM_FOREGROUND      = 0x0003;
    public const uint EVENT_OBJECT_LOCATIONCHANGE  = 0x800B;
    public const uint WINEVENT_OUTOFCONTEXT        = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS      = 0x0002;

    // SetWindowPos flags
    public const int SWP_NOSIZE       = 0x0001;
    public const int SWP_NOMOVE       = 0x0002;
    public const int SWP_NOZORDER     = 0x0004;
    public const int SWP_NOACTIVATE   = 0x0010;
    public const int SWP_SHOWWINDOW   = 0x0040;
    public const int SWP_HIDEWINDOW   = 0x0080;

    // Special HWND insert-after values for SetWindowPos
    public static readonly nint HWND_TOPMOST    = new nint(-1);
    public static readonly nint HWND_NOTOPMOST  = new nint(-2);

    // GetWindowLong / SetWindowLong indices
    public const int GWL_EXSTYLE = -20;

    // Extended window styles
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    public delegate void WinEventProc(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(
        uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

    [DllImport("user32.dll")]
    public static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    public const int SW_HIDE   = 0;
    public const int SW_SHOW   = 5;

    [DllImport("user32.dll")]
    public static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width  => Right - Left;
        public int Height => Bottom - Top;
    }
}
