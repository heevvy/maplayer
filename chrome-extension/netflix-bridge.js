window.addEventListener('message', function(e) {
  if (!e.data || e.data.source !== 'maplayer') return;
  try {
    var vp = netflix.appContext.state.playerApp.getAPI().videoPlayer;
    var sid = vp.getAllPlayerSessionIds()[0];
    if (!sid) return;
    var player = vp.getVideoPlayerBySessionId(sid);
    if (e.data.action === 'seek') player.seek(e.data.timeMs);
    if (e.data.action === 'playpause') {
      if (player.isPaused()) player.play(); else player.pause();
    }
  } catch(err) {}
});
