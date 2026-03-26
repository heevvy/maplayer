const btnPip = document.getElementById('btn-pip');
const siteName = document.getElementById('site-name');
const nativeStatus = document.getElementById('native-status');

let isPipActive = false;

async function getActiveTab() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  return tab;
}

async function updateStatus() {
  const tab = await getActiveTab();
  if (!tab?.id) return;

  chrome.tabs.sendMessage(tab.id, { type: 'get-status' }, (res) => {
    if (chrome.runtime.lastError || !res) {
      siteName.textContent = '지원하지 않는 사이트';
      btnPip.disabled = true;
      return;
    }
    siteName.textContent = res.site;
    btnPip.disabled = !res.hasVideo;
    isPipActive = res.isPip;
    btnPip.textContent = isPipActive ? 'PIP 중지' : 'PIP 시작';
    btnPip.classList.toggle('active', isPipActive);
  });

  chrome.runtime.sendMessage({ type: 'get-native-status' }, (res) => {
    if (chrome.runtime.lastError) return;
    const connected = res?.connected;
    nativeStatus.textContent = connected ? '준비됨' : '새로고침 필요';
    nativeStatus.className = connected ? 'connected' : 'disconnected';
  });
}

btnPip.addEventListener('click', async () => {
  const tab = await getActiveTab();
  if (!tab?.id) return;
  chrome.tabs.sendMessage(tab.id, { type: 'toggle-pip' }, () => updateStatus());
});

updateStatus();
setInterval(updateStatus, 2000);
