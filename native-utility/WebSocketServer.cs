using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PipPlayer;

internal class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _clients = new();
    private readonly PipWindowManager _pip;
    private readonly GameWindowTracker _game;
    private OverlayForm? _overlay;

    public void SetOverlay(OverlayForm overlay) => _overlay = overlay;

    public WebSocketServer(PipWindowManager pip, GameWindowTracker game, int port = 9877)
    {
        _pip = pip;
        _game = game;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _listener.Start();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                if (ctx.Request.IsWebSocketRequest)
                    _ = HandleClientAsync(ctx, ct);
                else
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
        }
    }

    private async Task HandleClientAsync(HttpListenerContext httpCtx, CancellationToken ct)
    {
        try
        {
            var wsCtx = await httpCtx.AcceptWebSocketAsync(null);
            var ws = wsCtx.WebSocket;
            lock (_clients) _clients.Add(ws);

            var buf = new byte[4096];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buf, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                HandleMessage(Encoding.UTF8.GetString(buf, 0, result.Count));
            }

            lock (_clients) _clients.Remove(ws);
            ws.Dispose();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "pip-state":
                    bool active = root.GetProperty("active").GetBoolean();
                    if (active)
                    {
                        Thread.Sleep(500);
                        _pip.FindNewPipWindow();
                    }
                    break;

                case "pip-prepare":
                    _pip.SnapshotChromeWindows();
                    break;

                case "playback-state":
                    if (_overlay != null)
                    {
                        _overlay.PlaybackTime = root.GetProperty("currentTime").GetDouble();
                        _overlay.PlaybackDuration = root.GetProperty("duration").GetDouble();
                        _overlay.PlaybackPaused = root.GetProperty("paused").GetBoolean();
                    }
                    break;

                case "set-opacity":
                    double val = root.GetProperty("value").GetDouble();
                    _pip.SetOpacity((byte)(val * 255));
                    break;

                case "set-position":
                    string preset = root.GetProperty("preset").GetString() ?? "top-right";
                    var pipRect = _pip.GetPipRect();
                    var g = _game.ClientBounds;
                    const int margin = 10;
                    var (px, py) = preset switch
                    {
                        "top-left" => (g.Left + margin, g.Top + margin),
                        "top-right" => (g.Right - pipRect.Width - margin, g.Top + margin),
                        "bottom-left" => (g.Left + margin, g.Bottom - pipRect.Height - margin),
                        "bottom-right" => (g.Right - pipRect.Width - margin, g.Bottom - pipRect.Height - margin),
                        _ => (g.Left + margin, g.Top + margin),
                    };
                    _pip.SetPosition(px, py);
                    break;

                case "set-size":
                    string size = root.GetProperty("preset").GetString() ?? "medium";
                    var (w, h) = size switch
                    {
                        "small" => (320, 180),
                        "medium" => (480, 270),
                        "large" => (640, 360),
                        _ => (480, 270),
                    };
                    _pip.SetSize(w, h);
                    break;
            }
        }
        catch { }
    }

    public async Task BroadcastAsync(string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        List<WebSocket> snapshot;
        lock (_clients) snapshot = new(_clients);

        foreach (var ws in snapshot)
        {
            if (ws.State == WebSocketState.Open)
            {
                try { await ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None); }
                catch { }
            }
        }
    }
}
