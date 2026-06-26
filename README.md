# MPLR

English | [简体中文](README.zh-Hans.md)

<img src="src/MPLR/Assets/Favicon.png" alt="MPLR icon" width="96" />

MPLR is a Windows-only WPF desktop app for live-room monitoring and unattended recording. It centers on quick room management, live preview, automatic recording, and local FFmpeg-based capture. The project continues from [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec), and the bundled Python resolver follows the maintained approach from [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder).

## What It Does

- Add live room links and monitor them from one list.
- Switch monitor, record, and notification behavior per room.
- Preview streams before recording and keep recording unattended.
- Use the bundled FFmpeg, FFprobe, and FFplay binaries.
- Resolve stream URLs through the included Python resolver when needed.
- Store config, logs, and cache locally under the app directory.
- Support platform cookies for cases where the platform requires them.

## Supported Platforms

- Douyin
- TikTok
- Douyu
- Huya
- Kuaishou
- Bilibili
- Twitch
- Xiaohongshu

## Requirements

- Windows
- .NET Desktop Runtime 9.0
- Python 3.11 or newer

## Build

```powershell
dotnet build .\src\MPLR\MPLR.csproj -c Debug -p:Platform=x64
```

## Run

```powershell
.\src\MPLR\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\MPLR.exe
```

## Packaging

```powershell
.\build\publish_win-x64.cmd
```

## Repository Layout

```text
src/MPLR              Main WPF application
docs                  Cookie and privacy docs
build                 Publish scripts and packaging assets
tools/stream_resolver Bundled Python resolver and vendor code
tools/ffmpeg/win-x64  FFmpeg, FFprobe, and FFplay binaries
```

## Documentation

- [Douyin cookie guide](docs/GETCOOKIE_DOUYIN.md)
- [TikTok cookie guide](docs/GETCOOKIE_TIKTOK.md)
- [Privacy policy](docs/PrivacyPolicy.zh-Hans.md)

## Credits

- [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)
- [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder)

## License

Released under the [MIT License](LICENSE).
