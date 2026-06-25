# MPLR

[简体中文](README.zh-Hans.md) | English

<img src="branding/logo.png" alt="MPLR logo" />

MPLR is a desktop live-stream recorder with a graphical interface, background monitoring, live preview, and FFmpeg-based recording. It started from [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec) and extends the recorder with a bundled Python stream resolver that references [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) and [wbt5/real-url](https://github.com/wbt5/real-url).

## Features

- WPF desktop app for Windows.
- Experimental Avalonia desktop app for future cross-platform work.
- Room list management with per-room monitor, record, and notification switches.
- Background polling and unattended recording when a room goes live.
- Live preview through FFplay or a configured player.
- Recording through FFmpeg with stream copy, optional proxy, custom User-Agent, segment recording, and optional post-conversion.
- Built-in fallback parsers for Douyin and TikTok.
- Bundled Python resolver for more platforms, including Bilibili, Douyu, Huya, Kuaishou, Twitch, YouTube, and other platforms supported by the resolver.
- Cookie and proxy settings for platforms that require login state, risk-control bypass, or regional access.

## Screenshots

<img src="assets/image-20241113165448238.png" alt="MPLR screenshot" />

## Requirements

Runtime:

- Windows: [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Other systems for the Avalonia prototype: [.NET Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Python 3.11 or newer is recommended for the bundled stream resolver.
- FFmpeg and FFplay are required for recording and preview. The Windows project includes packaged binaries under `tools/ffmpeg/win-x64`.

Windows development environment with Scoop:

```powershell
scoop bucket add versions
scoop bucket add extras
scoop install versions/dotnet9-sdk ffmpeg extras/vcredist2022 python311 nodejs
```

## Build And Run

Restore and build the Windows WPF app:

```powershell
dotnet build .\src\TiktokLiveRec.WPF\TiktokLiveRec.WPF.csproj -c Debug -p:Platform=x64
```

Run the generated app:

```powershell
.\src\TiktokLiveRec.WPF\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\MultiPlatformLiveRecorder.exe
```

Run the Avalonia prototype:

```powershell
dotnet run --project .\src\TiktokLiveRec.Avalonia\TiktokLiveRec.Avalonia.csproj
```

## Live Room URLs

Examples:

```text
https://live.douyin.com/123456
https://www.douyin.com/root/live/123456
https://www.tiktok.com/@example/live
https://live.bilibili.com/123456
https://www.douyu.com/123456
https://www.huya.com/123456
https://www.twitch.tv/example
https://www.youtube.com/watch?v=example
```

Some platforms may require cookies, a valid login session, a proxy, or a longer polling interval to avoid request throttling. See [GETCOOKIE_DOUYIN.md](doc/GETCOOKIE_DOUYIN.md) and [GETCOOKIE_TIKTOK.md](doc/GETCOOKIE_TIKTOK.md).

## Project Layout

```text
src/TiktokLiveRec.WPF             Windows WPF app, current primary UI
src/TiktokLiveRec.Avalonia        Avalonia desktop prototype
src/FluentAvaloniaUI.Violeta      Local Avalonia UI controls
tools/stream_resolver             Python stream resolver wrapper and vendored resolver code
tools/ffmpeg/win-x64              Windows FFmpeg, FFplay, and FFprobe binaries
doc                               Cookie and usage notes
build                             Publish scripts and packaging assets
```

## How It Works

1. The desktop UI stores rooms and user settings in a YAML configuration file.
2. `GlobalMonitor` periodically refreshes each room.
3. `Spider` first calls the bundled Python resolver. If it cannot resolve a stream, Douyin and TikTok fall back to the built-in C# parsers.
4. `Recorder` selects a usable FLV, HLS, or direct record URL and starts FFmpeg.
5. FFmpeg saves the stream as FLV or TS, optionally in segments, and optional conversion can run after recording.

## Platform Status

| Platform | Framework | Status |
| --- | --- | --- |
| Windows | WPF | Supported |
| Windows | Avalonia | Prototype |
| macOS | Avalonia | In progress |
| Linux | Avalonia | In progress |
| Android / iOS / tvOS | Avalonia or native | Experimental or planned |

## Credits And Upstream Projects

This repository builds on and references these projects:

- [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec): original GUI live recorder and project foundation.
- [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder): FFmpeg-based unattended multi-platform live recorder. Parts of the bundled resolver and parsing ideas reference this project.
- [wbt5/real-url](https://github.com/wbt5/real-url): Python implementations for extracting real live-stream URLs and danmaku from many live platforms. Resolver modules under `tools/stream_resolver/vendor/real_url` retain their upstream license.

## License

The main project is distributed under the [MIT License](LICENSE).

Vendored third-party code keeps its original license. In particular, `tools/stream_resolver/vendor/real_url` includes the GPL-2.0 license from `wbt5/real-url`, and `tools/stream_resolver/vendor/douyin_live_recorder` includes the MIT license from `ihmily/DouyinLiveRecorder`.
