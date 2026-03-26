namespace PipPlayer;

internal class GameWindowTracker
{
    public IntPtr GameHwnd { get; private set; }
    public Win32.RECT ClientBounds { get; private set; }
    public bool IsActive => GameHwnd != IntPtr.Zero && Win32.IsWindowVisible(GameHwnd);

    public event Action? GameWindowMoved;

    private static readonly string[] GameTitles = { "MapleStory" };
    private Win32.RECT _prevBounds;
    private IntPtr _winEventHook;
    private Win32.WinEventDelegate? _winEventProc;
    private PipWindowManager? _trackedPip;

    public void SetTrackedPip(PipWindowManager pip) { _trackedPip = pip; }

    public void Track()
    {
        if (GameHwnd != IntPtr.Zero && Win32.IsWindowVisible(GameHwnd))
        {
            UpdateClientBounds();
            return;
        }

        UninstallHook();
        GameHwnd = IntPtr.Zero;
        foreach (var title in GameTitles)
        {
            IntPtr hwnd = Win32.FindWindowW(null, title);
            if (hwnd != IntPtr.Zero && Win32.IsWindowVisible(hwnd))
            {
                GameHwnd = hwnd;
                UpdateClientBounds();
                _prevBounds = ClientBounds;
                InstallHook();
                return;
            }
        }
    }

    private void UpdateClientBounds()
    {
        if (GameHwnd == IntPtr.Zero) return;
        Win32.GetClientRect(GameHwnd, out var client);
        var topLeft = new Win32.POINT { X = 0, Y = 0 };
        Win32.ClientToScreen(GameHwnd, ref topLeft);
        ClientBounds = new Win32.RECT
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Right = topLeft.X + client.Width,
            Bottom = topLeft.Y + client.Height,
        };
    }

    private void InstallHook()
    {
        if (_winEventHook != IntPtr.Zero) return;
        Win32.GetWindowThreadProcessId(GameHwnd, out uint pid);
        _winEventProc = OnWinEvent;
        _winEventHook = Win32.SetWinEventHook(
            Win32.EVENT_OBJECT_LOCATIONCHANGE,
            Win32.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _winEventProc,
            pid, 0,
            Win32.WINEVENT_OUTOFCONTEXT);
    }

    private void UninstallHook()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd != GameHwnd || idObject != 0) return;
        UpdateClientBounds();

        if (_trackedPip != null)
        {
            FollowGameMovement(_trackedPip);
            ClampPipToGameWindow(_trackedPip);
            GameWindowMoved?.Invoke();
        }
    }

    public void FollowGameMovement(PipWindowManager pip)
    {
        if (!IsActive || !pip.IsActive) return;

        int dx = ClientBounds.Left - _prevBounds.Left;
        int dy = ClientBounds.Top - _prevBounds.Top;

        if (dx != 0 || dy != 0)
        {
            var r = pip.GetPipRect();
            pip.SetPosition(r.Left + dx, r.Top + dy);
        }

        _prevBounds = ClientBounds;
    }

    public void ClampPipToGameWindow(PipWindowManager pip)
    {
        if (!IsActive || !pip.IsActive) return;

        var pipRect = pip.GetPipRect();
        var game = ClientBounds;
        int w = pipRect.Width, h = pipRect.Height;
        bool resized = false;

        if (w > game.Width)
        {
            float aspect = (float)w / h;
            w = game.Width;
            h = (int)(w / aspect);
            resized = true;
        }
        if (h > game.Height)
        {
            float aspect = (float)w / h;
            h = game.Height;
            w = (int)(h * aspect);
            resized = true;
        }

        if (resized)
        {
            pip.SetSize(w, h);
            pipRect = pip.GetPipRect();
        }

        int newX = Math.Max(game.Left, Math.Min(pipRect.Left, game.Right - pipRect.Width));
        int newY = Math.Max(game.Top, Math.Min(pipRect.Top, game.Bottom - pipRect.Height));

        if (newX != pipRect.Left || newY != pipRect.Top)
            pip.SetPosition(newX, newY);
    }
}
