const WS_URL = 'ws://localhost:9877';

let wsRetryTimer = null;

function connectNative() {
  const ws = new WebSocket(WS_URL);

  ws.onopen = () => {
    console.log('[PIP Player] Native utility connected');
    if (wsRetryTimer) { clearInterval(wsRetryTimer); wsRetryTimer = null; }
  };

  ws.onmessage = async (event) => {
    try {
      const msg = JSON.parse(event.data);
      if (msg.type === 'seek' || msg.type === 'playpause') {
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tab?.id) chrome.tabs.sendMessage(tab.id, msg);
      }
    } catch {}
  };

  ws.onclose = () => {
    if (!wsRetryTimer) {
      wsRetryTimer = setInterval(() => connectNative(), 5000);
    }
  };

  ws.onerror = () => ws.close();

  return ws;
}

let nativeWs = null;

function ensureConnection() {
  if (!nativeWs || nativeWs.readyState !== WebSocket.OPEN) {
    nativeWs = connectNative();
  }
  return nativeWs;
}

function sendToNative(msg) {
  const ws = ensureConnection();
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(msg));
  }
}

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg.type === 'pip-state') {
    sendToNative({ type: 'pip-state', active: msg.active });
    chrome.storage.local.set({ pipActive: msg.active });
  }
  if (msg.type === 'pip-prepare') {
    sendToNative({ type: 'pip-prepare' });
  }
  if (msg.type === 'send-to-native') {
    sendToNative(msg.payload);
  }
  if (msg.type === 'get-native-status') {
    sendResponse({ connected: nativeWs?.readyState === WebSocket.OPEN });
    return true;
  }
});

chrome.runtime.onStartup.addListener(() => ensureConnection());
chrome.runtime.onInstalled.addListener(() => ensureConnection());
