(() => {
  const SITE_CONFIG = {
    'www.youtube.com': {
      videoSelector: 'video.html5-main-video',
      adSkipSelectors: ['.ytp-ad-skip-button-modern', '.ytp-skip-ad-button', 'button[aria-label^="Skip"]', '.ytp-ad-skip-button'],
      handleUnskippableAd(v) { if (v && document.querySelector('.ad-showing')) v.currentTime = v.duration || 0; },
    },
    'www.netflix.com': {
      videoSelector: 'video',
      adSkipSelectors: ['[data-uia="player-skip-intro"]', '[data-uia="player-skip-recap"]'],
    },
    'chzzk.naver.com': { videoSelector: 'video', adSkipSelectors: [] },
    'laftel.net': { videoSelector: 'video', adSkipSelectors: ['button.videoAdUiSkipButton'] },
    'wp.nexon.com': { videoSelector: 'video', adSkipSelectors: [] },
    'watcha.com': { videoSelector: 'video', adSkipSelectors: [] },
    'www.watcha.com': { videoSelector: 'video', adSkipSelectors: [] },
    'www.primevideo.com': { videoSelector: 'video', adSkipSelectors: ['.adSkipButton.skippable', '[data-testid="skip-ad-button"]', '.atvwebplayersdk-skipelements-button'] },
    'www.coupangplay.com': { videoSelector: 'video', adSkipSelectors: [] },
    'www.wavve.com': { videoSelector: 'video', adSkipSelectors: ['.btn_skip', 'button[class*="skip"]'] },
    'www.tving.com': { videoSelector: 'video', adSkipSelectors: ['button[class*="skip"]', 'button[class*="Skip"]'] },
    'www.disneyplus.com': { videoSelector: 'video', adSkipSelectors: ['.skip__button'] },
  };

  const SOOP_DEFAULT = { videoSelector: 'video', adSkipSelectors: [] };

  function isSoopHost(h) {
    if (h === 'sooplive.com' || h.endsWith('.sooplive.com')) return true;
    if (h === 'sooplive.co.kr' || h.endsWith('.sooplive.co.kr')) return true;
    return false;
  }

  function resolveConfig(h) {
    return SITE_CONFIG[h] || (isSoopHost(h) ? SOOP_DEFAULT : null);
  }

  const hostname = window.location.hostname;
  const config = resolveConfig(hostname);
  if (!config) return;

  const isNetflix = hostname === 'www.netflix.com';
  let pipActive = false;

  function findVideo() {
    return document.querySelector(config.videoSelector) || document.querySelector('video');
  }

  function trySkipAd() {
    for (const sel of config.adSkipSelectors) {
      const btn = document.querySelector(sel);
      if (btn && btn.offsetParent !== null) { btn.click(); return; }
    }
    if (config.handleUnskippableAd) config.handleUnskippableAd(findVideo());
  }

  function seekVideo(timeSec) {
    if (isNetflix) {
      window.postMessage({ source: 'maplayer', action: 'seek', timeMs: timeSec * 1000 }, '*');
      return;
    }
    try {
      const video = findVideo();
      if (video) video.currentTime = timeSec;
    } catch (e) {}
  }

  function playPauseVideo() {
    if (isNetflix) {
      window.postMessage({ source: 'maplayer', action: 'playpause' }, '*');
      return;
    }
    const video = findVideo();
    if (video) { if (video.paused) video.play(); else video.pause(); }
  }

  function togglePip() {
    const video = findVideo();
    if (!video) return Promise.reject(new Error('No video found'));
    if (document.pictureInPictureElement) return document.exitPictureInPicture();
    video.disablePictureInPicture = false;
    chrome.runtime.sendMessage({ type: 'pip-prepare' });
    return new Promise((resolve, reject) => {
      setTimeout(() => video.requestPictureInPicture().then(resolve).catch(reject), 300);
    });
  }

  setInterval(() => {
    if (!pipActive) return;
    const video = findVideo();
    if (!video) return;
    chrome.runtime.sendMessage({
      type: 'send-to-native',
      payload: {
        type: 'playback-state',
        currentTime: video.currentTime || 0,
        duration: video.duration || 0,
        paused: video.paused,
      },
    });
  }, 500);

  const adObserver = new MutationObserver(() => trySkipAd());
  adObserver.observe(document.documentElement, { childList: true, subtree: true });
  setInterval(trySkipAd, 2000);

  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg.type === 'toggle-pip') {
      togglePip()
        .then(() => sendResponse({ success: true, pip: !!document.pictureInPictureElement }))
        .catch(err => sendResponse({ success: false, error: err.message }));
      return true;
    }
    if (msg.type === 'get-status') {
      sendResponse({ hasVideo: !!findVideo(), isPip: !!document.pictureInPictureElement, site: hostname });
      return true;
    }
    if (msg.type === 'seek') {
      if (msg.time != null) seekVideo(msg.time);
    }
    if (msg.type === 'playpause') {
      playPauseVideo();
    }
  });

  document.addEventListener('enterpictureinpicture', () => {
    pipActive = true;
    chrome.runtime.sendMessage({ type: 'pip-state', active: true });
  });
  document.addEventListener('leavepictureinpicture', () => {
    pipActive = false;
    chrome.runtime.sendMessage({ type: 'pip-state', active: false });
  });
})();
