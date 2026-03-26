namespace PipPlayer;

internal class PipWindowManager
{
    public IntPtr PipHwnd { get; private set; }
    public bool IsActive => PipHwnd != IntPtr.Zero && Win32.IsWindowVisible(PipHwnd);

    private byte _currentAlpha = 242;
    public byte CurrentAlpha => _currentAlpha;

    private HashSet<IntPtr> _knownWindows = new();

    public void SnapshotChromeWindows()
    {
        _knownWindows.Clear();
        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            string? exePath = Win32.GetProcessName(pid);
            if (exePath == null) return true;
            string exeName = Path.GetFileName(exePath).ToLowerInvariant();
            if (exeName == "chrome.exe" || exeName == "msedge.exe")
                _knownWindows.Add(hwnd);
            return true;
        }, IntPtr.Zero);
    }

    public void FindNewPipWindow()
    {
        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;
            if (_knownWindows.Contains(hwnd)) return true;

            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            string? exePath = Win32.GetProcessName(pid);
            if (exePath == null) return true;
            string exeName = Path.GetFileName(exePath).ToLowerInvariant();
            if (exeName != "chrome.exe" && exeName != "msedge.exe") return true;

            Win32.GetWindowRect(hwnd, out var rect);
            if (rect.Width < 100 || rect.Height < 60) return true;

            PipHwnd = hwnd;
            ApplyOverlayStyles();
            return false;
        }, IntPtr.Zero);
    }

    public void CheckAlive()
    {
        if (PipHwnd != IntPtr.Zero && !Win32.IsWindowVisible(PipHwnd))
            PipHwnd = IntPtr.Zero;
    }

    public void ScanForPipWindow()
    {
        IntPtr found = IntPtr.Zero;

        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;

            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            string? exePath = Win32.GetProcessName(pid);
            if (exePath == null) return true;

            string exeName = Path.GetFileName(exePath).ToLowerInvariant();
            if (exeName != "chrome.exe" && exeName != "msedge.exe") return true;

            string title = Win32.GetWindowTitle(hwnd);
            if (!string.IsNullOrEmpty(title)) return true;

            Win32.GetWindowRect(hwnd, out var rect);
            if (rect.Width > 1200 || rect.Height > 900) return true;
            if (rect.Width < 100 || rect.Height < 60) return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        if (found != IntPtr.Zero)
        {
            PipHwnd = found;
            ApplyOverlayStyles();
        }
    }

    public void ApplyOverlayStyles()
    {
        if (PipHwnd == IntPtr.Zero) return;

        int exStyle = Win32.GetWindowLongW(PipHwnd, Win32.GWL_EXSTYLE);
        int desired = exStyle | Win32.WS_EX_LAYERED | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT;

        if (exStyle != desired)
            Win32.SetWindowLongW(PipHwnd, Win32.GWL_EXSTYLE, desired);

        Win32.SetLayeredWindowAttributes(PipHwnd, 0, _currentAlpha, Win32.LWA_ALPHA);
    }

    public void SetOpacity(byte alpha)
    {
        _currentAlpha = Math.Max((byte)13, Math.Min((byte)242, alpha));
        if (PipHwnd != IntPtr.Zero)
            Win32.SetLayeredWindowAttributes(PipHwnd, 0, _currentAlpha, Win32.LWA_ALPHA);
    }

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    public void SetPosition(int x, int y)
    {
        if (PipHwnd == IntPtr.Zero) return;
        Win32.SetWindowPos(PipHwnd, HWND_TOP, x, y, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    public void SetSize(int w, int h)
    {
        if (PipHwnd == IntPtr.Zero) return;
        Win32.GetWindowRect(PipHwnd, out var rect);
        Win32.SetWindowPos(PipHwnd, HWND_TOP, rect.Left, rect.Top, w, h,
            Win32.SWP_NOACTIVATE);
    }

    public Win32.RECT GetPipRect()
    {
        if (PipHwnd == IntPtr.Zero) return default;
        Win32.GetWindowRect(PipHwnd, out var rect);
        return rect;
    }
}
