using System.Windows.Forms;
using PipPlayer;

if (!Startup.EnsureInstalled()) return;

using var mutex = new Mutex(true, "Maplayer_SingleInstance", out bool isNew);
if (!isNew) return;

Application.EnableVisualStyles();
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

var pip = new PipWindowManager();
var game = new GameWindowTracker();
var wsServer = new WebSocketServer(pip, game);
var overlay = new OverlayForm(pip, game);
wsServer.SetOverlay(overlay);

var seekTimer = new System.Windows.Forms.Timer { Interval = 50 };
seekTimer.Tick += async (_, _) =>
{
    var seekTime = overlay.ConsumeSeekRequest();
    if (seekTime.HasValue)
    {
        await wsServer.BroadcastAsync(
            System.Text.Json.JsonSerializer.Serialize(new { type = "seek", time = seekTime.Value }));
    }
};
seekTimer.Start();

_ = Task.Run(async () =>
{
    try { await wsServer.StartAsync(CancellationToken.None); }
    catch { }
});

Application.Run(overlay);
