# Maplayer

게임 위에 스트리밍 영상을 오버레이하는 PIP(Picture-in-Picture) 플레이어입니다.

Chrome 확장 프로그램으로 영상을 PIP 모드로 전환하고, Windows 네이티브 유틸리티가 게임 창 위에 항상 표시되도록 관리합니다.

Chrome, Edge, Whale 등 크로미움 기반 브라우저에서 사용할 수 있습니다.

## 주요 기능

- 한 클릭으로 스트리밍 영상을 PIP 전환
- 게임 창 위에 항상 표시 (Always-on-Top)
- 클릭 투과 모드 (게임 조작 방해 없음)
- 투명도 조절
- 광고 자동 스킵 (YouTube, Netflix, Prime Video 등)
- 재생 컨트롤 (재생/일시정지, 탐색)

## 지원 사이트

| 사이트 | 광고 스킵 |
|--------|-----------|
| YouTube | O |
| Netflix | O (인트로/요약 스킵) |
| Disney+ | O |
| Amazon Prime Video | O |
| Wavve | O |
| Tving | O |
| Laftel | O |
| SOOP (`*.sooplive.com`, `*.sooplive.co.kr`) | - |
| Chzzk | - |
| WATCHA | - |
| Coupang Play | - |
| 넥슨라이브 (wp.nexon.com) | - |

## 설치 방법

### 확장 프로그램 (Chrome / Edge / Whale)

**Chrome 웹 스토어** (권장):
> 스토어 승인 후 링크가 추가됩니다.

**수동 설치** (개발자 모드):
1. [Releases](../../releases) 페이지에서 `Maplayer_Chrome_Extension.zip` 다운로드
2. 압축 해제
3. 확장 프로그램 관리 페이지 열기:
   - Chrome: `chrome://extensions`
   - Edge: `edge://extensions`
   - Whale: `whale://extensions`
4. 개발자 모드 켜기
5. "압축해제된 확장 프로그램을 로드합니다" → 압축 해제한 폴더 선택

> Edge에서 Chrome 웹 스토어를 이용하려면 확장 페이지에서 "다른 스토어의 확장 허용"을 켜세요.

### 네이티브 유틸리티 (Windows)

게임 위 오버레이, 클릭 투과, 투명도 등 Windows 레벨 기능을 담당합니다.

1. [Releases](../../releases) 페이지에서 `Maplayer_Setup.exe` 다운로드
2. 실행하면 자동으로 `%LocalAppData%\Maplayer`에 설치
3. Windows 시작 시 자동 실행 설정됨

> **SmartScreen 경고가 뜨는 경우**: "추가 정보" → "실행"을 누르세요.
> 코드 서명이 없는 개인 개발 프로그램이기 때문에 나타나는 경고이며, 소스 코드가 100% 공개되어 있으므로 직접 확인하실 수 있습니다.

## 직접 빌드하기

소스 코드를 직접 빌드하고 싶은 경우:

### Chrome 확장

`chrome-extension/` 폴더를 그대로 Chrome에 로드하면 됩니다.

### 네이티브 유틸리티

**.NET 8 SDK**가 필요합니다.

```powershell
cd native-utility
dotnet publish -c Release
```

빌드 결과: `bin/Release/net8.0-windows/win-x64/publish/PipPlayer.exe`

### 파일 무결성 검증

Releases 페이지에 SHA-256 해시가 첨부됩니다. 다운로드한 파일의 해시를 비교하여 변조 여부를 확인할 수 있습니다.

```powershell
Get-FileHash -Algorithm SHA256 .\Maplayer_Setup.exe
```

## 구조

```
pip-player/
├── chrome-extension/    # Chrome 확장 프로그램
│   ├── manifest.json
│   ├── content.js       # 사이트별 비디오 감지 및 PIP 제어
│   ├── service-worker.js # WebSocket 통신 (네이티브 유틸리티 연동)
│   ├── popup.html/js/css # 팝업 UI
│   └── icons/
├── native-utility/      # C# Windows 네이티브 유틸리티
│   ├── Program.cs
│   ├── WebSocketServer.cs
│   ├── PipWindowManager.cs
│   ├── GameWindowTracker.cs
│   ├── OverlayForm.cs
│   ├── Startup.cs       # 자동 설치 및 시작 프로그램 등록
│   └── Win32Api.cs
└── privacy-policy/      # 개인정보처리방침
```

## 동작 원리

1. Chrome 확장이 스트리밍 사이트에서 비디오를 감지
2. PIP 버튼 클릭 → 브라우저 내장 PIP API로 영상 전환
3. 확장의 Service Worker가 WebSocket(`localhost:9877`)으로 네이티브 유틸리티에 연결
4. 네이티브 유틸리티가 PIP 창을 추적하여 게임 창 위에 항상 표시되도록 관리

## 라이선스

MIT License
