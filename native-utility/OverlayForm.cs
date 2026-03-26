using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PipPlayer;

internal enum DragMode { None, Move, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight }

internal class OverlayForm : Form
{
    private const int Border = 15;
    private const int Corner = 22;
    private const int MinW = 200;
    private const int MinH = 120;
    private const int TrackThick = 5;

    private readonly PipWindowManager _pip;
    private readonly GameWindowTracker _game;
    private readonly System.Windows.Forms.Timer _pollTimer;

    private enum Mode { None, Move, Resize, Seek, Opacity }
    private Mode _mode = Mode.None;
    private DragMode _resizeDir;
    private Point _dragStart;
    private float _aspectRatio = 16f / 9f;

    public double PlaybackTime { get; set; }
    public double PlaybackDuration { get; set; }
    public bool PlaybackPaused { get; set; }

    private float _opacityLevel = 0.95f;
    private double _lastRenderedTime;
    private float _lastRenderedOpacity;

    public OverlayForm(PipWindowManager pip, GameWindowTracker game)
    {
        _pip = pip;
        _game = game;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Visible = false;

        Size = new Size(1, 1);
        Location = new Point(-100, -100);

        _game.SetTrackedPip(_pip);
        _game.GameWindowMoved += () =>
        {
            SyncPosition();
            if (Visible)
                Win32.SetWindowPos(Handle, new IntPtr(-1),
                    0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        };

        _pollTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _pollTimer.Tick += (_, _) => PollTick();
        _pollTimer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Win32.WS_EX_LAYERED | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e) { }

    private void RenderOverlay()
    {
        int w = Width, h = Height;
        if (w < 10 || h < 10) return;

        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(0, 0, 0, 0));

            using var ghostBrush = new SolidBrush(Color.FromArgb(1, 0, 0, 0));
            g.FillRectangle(ghostBrush, 0, 0, w, Border);
            g.FillRectangle(ghostBrush, 0, Border, Border, h - Border * 2);
            g.FillRectangle(ghostBrush, 0, h - Border, Corner, Border);
            g.FillRectangle(ghostBrush, w - Corner, 0, Corner, Border);
            g.FillRectangle(ghostBrush, w - Border, h - Corner, Border, Corner);
            g.FillRectangle(ghostBrush, w - Corner, h - Border, Corner, Border);
            g.FillRectangle(ghostBrush, Corner, h - Border, w - Corner * 2, Border);
            g.FillRectangle(ghostBrush, w - Border, Corner, Border, h - Corner * 2);

            int trackMargin = (Border - TrackThick) / 2;

            int btY = h - Border + trackMargin;
            using (var trackBrush = new SolidBrush(Color.FromArgb(120, 100, 100, 100)))
                g.FillRectangle(trackBrush, Corner, btY, w - Corner * 2, TrackThick);

            if (PlaybackDuration > 0)
            {
                float progress = (float)(PlaybackTime / PlaybackDuration);
                int fillW = (int)((w - Corner * 2) * progress);
                using var barBrush = new SolidBrush(Color.FromArgb(240, 70, 130, 255));
                g.FillRectangle(barBrush, Corner, btY, fillW, TrackThick);
            }

            int rtX = w - Border + trackMargin;
            int rtTop = Corner;
            int rtH = h - Corner * 2;
            using (var trackBrush = new SolidBrush(Color.FromArgb(120, 100, 100, 100)))
                g.FillRectangle(trackBrush, rtX, rtTop, TrackThick, rtH);

            float displayRatio = (_opacityLevel - 0.05f) / 0.9f;
            int filledH = (int)(rtH * displayRatio);
            using (var barBrush = new SolidBrush(Color.FromArgb(240, 255, 190, 50)))
                g.FillRectangle(barBrush, rtX, rtTop + rtH - filledH, TrackThick, filledH);
        }

        ApplyBitmapToLayeredWindow(bmp);
    }

    private void ApplyBitmapToLayeredWindow(Bitmap bmp)
    {
        IntPtr screenDc = IntPtr.Zero, memDc = IntPtr.Zero, hBitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;

        try
        {
            screenDc = CreateGraphics().GetHdc();
            memDc = Win32.CreateCompatibleDC(screenDc);
            hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            oldBitmap = Win32.SelectObject(memDc, hBitmap);

            var ptDst = new Win32.POINT { X = Left, Y = Top };
            var size = new Win32.SIZE { cx = Width, cy = Height };
            var ptSrc = new Win32.POINT { X = 0, Y = 0 };
            var blend = new Win32.BLENDFUNCTION
            {
                BlendOp = Win32.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = Win32.AC_SRC_ALPHA,
            };

            Win32.UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, Win32.ULW_ALPHA);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero) Win32.SelectObject(memDc, oldBitmap);
            if (hBitmap != IntPtr.Zero) Win32.DeleteObject(hBitmap);
            if (memDc != IntPtr.Zero) Win32.DeleteDC(memDc);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var zone = GetZone(e.Location);

        if (e.Button == MouseButtons.Left)
        {
            if (zone == HitZone.Border || zone == HitZone.BottomBar || zone == HitZone.RightBar)
            {
                _mode = Mode.Move;
                _dragStart = PointToScreen(e.Location);
                Capture = true;
            }
            else if (zone == HitZone.Corner)
            {
                _mode = Mode.Resize;
                _resizeDir = WhichCorner(e.Location);
                _dragStart = PointToScreen(e.Location);
                CaptureAspect();
                Capture = true;
            }
        }
        else if (e.Button == MouseButtons.Middle)
        {
            if (zone == HitZone.BottomBar)
            {
                _playPauseRequested = true;
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (zone == HitZone.BottomBar)
            {
                _mode = Mode.Seek;
                DoSeek(e.Location);
                Capture = true;
            }
            else if (zone == HitZone.RightBar)
            {
                _mode = Mode.Opacity;
                DoOpacity(e.Location);
                Capture = true;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        switch (_mode)
        {
            case Mode.Move:
                var sp = PointToScreen(e.Location);
                DoMove(sp.X - _dragStart.X, sp.Y - _dragStart.Y);
                _dragStart = sp;
                break;
            case Mode.Resize:
                var sp2 = PointToScreen(e.Location);
                DoResize(sp2.X - _dragStart.X, sp2.Y - _dragStart.Y);
                _dragStart = sp2;
                break;
            case Mode.Seek:
                DoSeek(e.Location);
                break;
            case Mode.Opacity:
                DoOpacity(e.Location);
                break;
            case Mode.None:
                UpdateCursor(e.Location);
                break;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_mode != Mode.None) { _mode = Mode.None; Capture = false; }
    }

    private double? _seekRequested;
    public double? ConsumeSeekRequest()
    {
        var v = _seekRequested;
        _seekRequested = null;
        return v;
    }

    private bool _playPauseRequested;
    public bool ConsumePlayPauseRequest()
    {
        var v = _playPauseRequested;
        _playPauseRequested = false;
        return v;
    }

    private void DoSeek(Point pt)
    {
        if (PlaybackDuration <= 0) return;
        int barLeft = Corner;
        int barWidth = ClientSize.Width - Corner * 2;
        float ratio = Math.Clamp((float)(pt.X - barLeft) / barWidth, 0f, 1f);
        PlaybackTime = ratio * PlaybackDuration;
        _seekRequested = PlaybackTime;
        RenderOverlay();
    }

    private void DoOpacity(Point pt)
    {
        int barTop = Corner;
        int barHeight = ClientSize.Height - Corner * 2;
        float ratio = 1f - Math.Clamp((float)(pt.Y - barTop) / barHeight, 0f, 1f);
        _opacityLevel = 0.05f + ratio * 0.9f;
        _pip.SetOpacity((byte)(_opacityLevel * 255));
        RenderOverlay();
    }

    private void CaptureAspect()
    {
        var r = _pip.GetPipRect();
        if (r.Height > 0) _aspectRatio = (float)r.Width / r.Height;
    }

    private void DoMove(int dx, int dy)
    {
        var r = _pip.GetPipRect();
        int x = r.Left + dx, y = r.Top + dy;
        Clamp(ref x, ref y, r.Width, r.Height);
        _pip.SetPosition(x, y);
        SyncPosition();
    }

    private void DoResize(int dx, int dy)
    {
        var r = _pip.GetPipRect();
        int x = r.Left, y = r.Top, w = r.Width, h = r.Height;
        int d = Math.Abs(dx) > Math.Abs(dy) ? dx : (int)(dy * _aspectRatio);
        switch (_resizeDir)
        {
            case DragMode.ResizeBottomRight: w = Math.Max(MinW, w + d); break;
            case DragMode.ResizeTopLeft: w = Math.Max(MinW, w - d); x = r.Right - w; break;
            case DragMode.ResizeTopRight: w = Math.Max(MinW, w + d); break;
            case DragMode.ResizeBottomLeft: w = Math.Max(MinW, w - d); x = r.Right - w; break;
        }
        h = (int)(w / _aspectRatio);
        if (_resizeDir == DragMode.ResizeTopLeft || _resizeDir == DragMode.ResizeTopRight) y = r.Bottom - h;
        if (_game.IsActive) { w = Math.Min(w, _game.ClientBounds.Width); h = (int)(w / _aspectRatio); }
        Clamp(ref x, ref y, w, h);
        _pip.SetSize(w, h);
        _pip.SetPosition(x, y);
        SyncPosition();
    }

    private void Clamp(ref int x, ref int y, int w, int h)
    {
        if (!_game.IsActive) return;
        var g = _game.ClientBounds;
        x = Math.Max(g.Left, Math.Min(x, g.Right - w));
        y = Math.Max(g.Top, Math.Min(y, g.Bottom - h));
    }

    private void UpdateCursor(Point pt)
    {
        var z = GetZone(pt);
        Cursor = z switch
        {
            HitZone.Corner => WhichCorner(pt) switch
            {
                DragMode.ResizeTopLeft or DragMode.ResizeBottomRight => Cursors.SizeNWSE,
                _ => Cursors.SizeNESW,
            },
            HitZone.Border => Cursors.SizeAll,
            HitZone.BottomBar or HitZone.RightBar => Cursors.Hand,
            _ => Cursors.Default,
        };
    }

    public void SyncPosition()
    {
        if (!_pip.IsActive) { if (Visible) Visible = false; return; }
        var r = _pip.GetPipRect();
        if (r.Width < 10 || r.Height < 10) { if (Visible) Visible = false; return; }
        if (!Visible) { Visible = true; CaptureAspect(); }

        bool posChanged = Left != r.Left || Top != r.Top;
        bool sizeChanged = Width != r.Width || Height != r.Height;
        if (posChanged || sizeChanged)
            SetBounds(r.Left, r.Top, r.Width, r.Height);
        if (sizeChanged)
            RenderOverlay();
    }

    private void PollTick()
    {
        _game.Track();
        _pip.CheckAlive();

        if (_pip.IsActive)
        {
            _pip.ApplyOverlayStyles();
            if (_mode == Mode.None)
            {
                _game.ClampPipToGameWindow(_pip);
                SyncPosition();

                if (Visible)
                    Win32.SetWindowPos(Handle, new IntPtr(-1),
                        0, 0, 0, 0,
                        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }

            if (Math.Abs(PlaybackTime - _lastRenderedTime) > 0.3 || Math.Abs(_opacityLevel - _lastRenderedOpacity) > 0.01)
            {
                _lastRenderedTime = PlaybackTime;
                _lastRenderedOpacity = _opacityLevel;
                RenderOverlay();
            }
        }
        else
        {
            if (Visible) Visible = false;
        }
    }

    private enum HitZone { Outside, Interior, Border, Corner, BottomBar, RightBar }

    private HitZone GetZone(Point pt)
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        int dL = pt.X, dR = w - pt.X, dT = pt.Y, dB = h - pt.Y;

        bool nearEdge = dL < Border || dR < Border || dT < Border || dB < Border;
        if (!nearEdge) return HitZone.Interior;

        bool hC = dL < Corner || dR < Corner;
        bool vC = dT < Corner || dB < Corner;
        if (hC && vC) return HitZone.Corner;

        if (dB < Border && dL >= Corner && dR >= Corner) return HitZone.BottomBar;
        if (dR < Border && dT >= Corner && dB >= Corner) return HitZone.RightBar;

        return HitZone.Border;
    }

    private DragMode WhichCorner(Point pt)
    {
        bool left = pt.X < ClientSize.Width / 2;
        bool top = pt.Y < ClientSize.Height / 2;
        return (top, left) switch
        {
            (true, true) => DragMode.ResizeTopLeft,
            (true, false) => DragMode.ResizeTopRight,
            (false, true) => DragMode.ResizeBottomLeft,
            (false, false) => DragMode.ResizeBottomRight,
        };
    }
}
