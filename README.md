# MPLR

English | [简体中文](README.zh-Hans.md)

<img src="src/MPLR/Assets/Favicon.png" alt="MPLR icon" width="96" />

MPLR is a Windows desktop live-stream recorder focused on room monitoring, live preview, and unattended FFmpeg recording. The current application is WPF-only and continues from [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec), while the bundled Python stream resolver follows the maintained approach from [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder).

## Features

- Single Windows WPF entry point.
- Room list management with independent monitor, record, and notify switches.
- Auto polling, auto record, and live preview.
- Built-in FFmpeg, FFprobe, and FFplay portable packaging.
- Portable `config`, `logs`, and `cache` folders under the app root.
- Real-time session logging, interaction logging, and config change logging.
- Stream preview header completion for `User-Agent`, `Referer`, and related request headers.
- Built-in Douyin and TikTok fallback parsers plus a bundled Python multi-platform resolver.

## Requirements

- Windows
- [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Python 3.11 or newer for the bundled resolver

Optional local development tools:

```powershell
scoop bucket add versions
scoop bucket add extras
scoop install versions/dotnet9-sdk ffmpeg extras/vcredist2022 python311 nodejs
```

## Build

```powershell
dotnet build .\src\MPLR\MPLR.csproj -c Debug -p:Platform=x64
```

Run:

```powershell
.\src\MPLR\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\MPLR.exe
```

## Project Layout

```text
src/MPLR                    Windows WPF application
docs                        User documentation
build                       Packaging scripts and assets
tools/stream_resolver       Bundled Python stream resolver
tools/ffmpeg/win-x64        FFmpeg, FFprobe, FFplay binaries
```

## Credits

- [qzj1472/MPLR](https://github.com/qzj1472/MPLR)
- [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)
- [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder)

## License

Released under the [MIT License](LICENSE).
