# Xibo Windows Player — Developer Guide

## Project Overview

Xibo for Windows is a .NET Framework WPF digital signage player designed for 24/7 kiosk operation. It connects to a Xibo CMS via the XMDS SOAP API (v7) to download schedules and media, then plays content in a full-screen loop.

- **Client version:** 4 R407.0 (code version 407, API version 7)
- **Framework:** .NET Framework 4.7.2, WPF, Windows-only
- **Screensaver mode:** The binary is also copied as `Xibo.scr` for use as a Windows screensaver

## Requirements

- Windows 10 or later
- .NET Framework 4.7.2
- Visual Studio 2019+ with the **.NET desktop development** workload
- NuGet packages are restored automatically on build

## Building

1. Open `XiboClient.sln` in Visual Studio
2. NuGet packages restore automatically
3. Build for **AnyCPU** or **x86**, **Debug** or **Release**
4. Output: `XiboClient.exe` — the post-build event also copies it to `Xibo.scr`

## Architecture

### Rendering hierarchy

```
MainWindow (WPF Window)
  └─ Layout (UserControl)          — one active layout at a time (+ overlay layouts)
       └─ Region (UserControl)     — one per zone defined in the layout XML
            └─ Media (UserControl) — items play in sequence within each region
                 ├─ Image
                 ├─ Video / Audio
                 ├─ WebCef (CefSharp/Chromium)
                 ├─ WebEdge (WebView2/Edge)
                 ├─ WebIe (Internet Explorer)
                 ├─ PowerPoint
                 ├─ Flash
                 ├─ ShellCommand
                 └─ Spacer
```

### Background agent threads (`XmdsAgents/`)

| Agent | Purpose |
|-------|---------|
| `RegisterAgent` | Player registration, hardware ID, licensing |
| `ScheduleAndFilesAgent` | Downloads schedule XML and required file manifest |
| `FileAgent` | Per-file chunked downloader with CRC32 validation |
| `DataAgent` | Widget dynamic data updates |
| `StatAgent` | Batched proof-of-play upload |
| `LibraryAgent` | Local media library maintenance (runs every 2 min) |
| `LogAgent` | Log file upload |
| `FaultsAgent` | Error reporting |

### Key singletons

- `Logic/ApplicationSettings.cs` — all configuration, loaded from XML/JSON/registry
- `Logic/CacheManager.cs` — file cache tracking (MD5/CRC32)
- `Stats/StatManager.cs` — proof-of-play and engagement statistics
- `Log/ClientInfo.cs` — runtime environment information

### Real-time messaging

`Action/XmrSubscriber.cs` handles live commands from the CMS (layout changes, screenshots, resets) via WebSocket or ZeroMQ (NetMQ).

### Local HTTP control server

`Control/EmbeddedServer.cs` runs on port 9696 (configurable). Endpoints include player info, webhooks, fault reporting, criteria validation, and trigger events.

## Key Files

| File | Purpose |
|------|---------|
| `App.xaml.cs` | Entry point; screensaver arg detection, startup |
| `MainWindow.xaml.cs` | Main player window; layout switching, kiosk management, power control |
| `Logic/ApplicationSettings.cs` | Singleton config (~730 lines) |
| `Logic/ScheduleManager.cs` | Schedule polling, geo-criteria matching, adspace (~2000 lines) |
| `Logic/CacheManager.cs` | File cache tracking |
| `Rendering/Layout.xaml.cs` | Layout XML parsing, background, duration calculation |
| `Rendering/Region.xaml.cs` | Media sequence playback within a zone |
| `Rendering/Media.xaml.cs` | Base class for all media types; timer, stats, expiry |
| `Stats/StatManager.cs` | Statistics collection, aggregation, upload |
| `Action/XmrSubscriber.cs` | Real-time CMS commands |
| `Control/EmbeddedServer.cs` | Local HTTP server (EmbedIO) |
| `default.config.xml` | Default settings template (68 settings) |

## Configuration

Default settings live in `default.config.xml`. At runtime the player writes settings to the user's AppData folder. Notable settings:

| Setting | Description | Default |
|---------|-------------|---------|
| `ServerUri` | CMS base URL | — |
| `ServerKey` | CMS API key | — |
| `LibraryPath` | Media cache directory | AppData |
| `VideoRenderingEngine` | `WindowsMediaPlayer` or `DirectShow` | WindowsMediaPlayer |
| `UseCefWebBrowser` | Use CefSharp (Chromium) for web content | false |
| `FallbackToEdge` | Use WebView2 (Edge) for web content | false |
| `EmbeddedServerPort` | Local control server port | 9696 |
| `StatsEnabled` | Proof-of-play tracking | true |
| `LogLevel` | `error` \| `info` \| `audit` \| `debug` | error |
| `MaxConcurrentDownloads` | Parallel file downloads | 5 |
| `PreventSleep` | Block Windows sleep/display-off | true |

## Web Rendering Engines

Three engines are available; selection is controlled by settings:

1. **CefSharp** (Chromium v141) — set `UseCefWebBrowser=true`
2. **WebView2** (Edge) — set `FallbackToEdge=true` (used when CEF is unavailable)
3. **Internet Explorer** — legacy fallback when neither CEF nor Edge is enabled

## Logging

- Primary: in-memory circular buffer via `XiboTraceListener`
- Optional disk output: configure `LogToDiskLocation`
- Also writes to Windows Event Log

## Content Types

| Type | Class | Notes |
|------|-------|-------|
| Images (JPG, PNG, GIF) | `Rendering/Image.cs` | BitmapImage with scale options |
| Video (MP4, AVI, etc.) | `Rendering/Video.cs` | MediaElement or WMP |
| Audio (MP3, WAV, etc.) | `Rendering/Audio.cs` | No visual output |
| Web / HTML | `Rendering/WebCef.cs`, `WebEdge.cs`, `WebIe.cs` | Selectable engine |
| PowerPoint | `Rendering/PowerPoint.cs` | COM interop |
| Shell commands | `Rendering/ShellCommand.cs` | Process execution |

## Statistics & Proof of Play

`Stats/StatManager.cs` records layout and media playback durations, interactive events, and geo-location data. Stats are aggregated at individual/hourly/daily granularity and uploaded to the CMS via XMDS `SubmitStats()`.

## Branches

| Branch | Purpose |
|--------|---------|
| `master` | Stable v4 |
| `develop` | Next release work-in-progress |
| `feature/finlay` | v3 R300 development |
| `release/winforms` | v2 — Windows Forms based (up to R202) |
| `release/tempel` | v1.8 |
| `release/tuttle` | v1.7 |

## External Resources

- Product website: https://xibosignage.com
- Community support: https://community.xibo.org.uk
- Issue tracker: GitHub Issues (verified bugs only — use community forum first)
- Contribution guide: `CONTRIBUTING.md`
- License: GNU Affero General Public License v3 (`LICENSE`)
