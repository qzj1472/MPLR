[English](README.md) | [简体中文](README.zh-Hans.md)

<img src="branding/logo.png" />

# Multi-platform Live Recorder

[![GitHub license](https://img.shields.io/github/license/emako/TiktokLiveRec)](https://github.com/emako/TiktokLiveRec/blob/master/LICENSE) [![Actions](https://github.com/emako/TiktokLiveRec/actions/workflows/build.yml/badge.svg)](https://github.com/emako/TiktokLiveRec/actions/workflows/library.nuget.yml) [![Platform](https://img.shields.io/badge/platform-Windows-blue?logo=windowsxp&color=1E9BFA)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) [![GitHub downloads](https://img.shields.io/github/downloads/emako/TiktokLiveRec/total)](https://github.com/emako/TiktokLiveRec/releases)
[![GitHub downloads](https://img.shields.io/github/downloads/emako/TiktokLiveRec/latest/total)](https://github.com/emako/TiktokLiveRec/releases)

With a graphical UI, unattended operation, live stream preview, and live stream recording capabilities.

Based on FFmpeg and FFplay.

## Screen Shot

<img src="assets/image-20241113165355466.png" alt="image-20241113165355466" style="transform:scale(0.5);" />

## Runtime Requirements

For Windows: [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

For Others: [.NET Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

For local development on Windows with Scoop:

```powershell
scoop bucket add versions
scoop bucket add extras
scoop install versions/dotnet9-sdk ffmpeg extras/vcredist2022 python311 nodejs
```

## Live Streaming

The built-in C# fallback supports Douyin and TikTok. The bundled Python stream resolver extends recording support to more platforms.

| Site | Status |
| ---- | ------ |
| Douyin | Available |
| TikTok | Available |
| Bilibili | Available through stream resolver |
| Kuaishou | Available through stream resolver |
| Douyu | Available through stream resolver |
| Huya | Available through stream resolver |
| Twitch | Available through stream resolver |
| YouTube and other resolver-supported sites | Available when the resolver can return a stream |

How to add live room:

```bash
# Douyin room URL like following:
https://live.douyin.com/XXX
https://www.douyin.com/root/live/XXX

# TikTok room URL like following:
https://www.tiktok.com/@XXX/live

# Other platforms can usually be added with their public live room URL:
https://live.bilibili.com/XXX
https://www.douyu.com/XXX
https://www.huya.com/XXX
https://www.twitch.tv/XXX
```

## Support OS

For rapid development, first implement WPF-based Windows support.

Other systems are planned around personal needs and user feedback.

| OS | Framework | Status |
| -- | --------- | ------ |
| Windows | WPF | Available |
| macOS | Avalonia | Under Development |
| Ubuntu | Avalonia | TBD |
| Android | Avalonia | TBD |
| iOS | Avalonia | TBD |
| tvOS | TBD | TBD |

## Cookies

Some platforms may require cookies, login state, or a proxy to resolve streams reliably. Check [GETCOOKIE_DOUYIN.md](doc/GETCOOKIE_DOUYIN.md) or [GETCOOKIE_TIKTOK.md](doc/GETCOOKIE_TIKTOK.md) for examples.

## Privacy Policy

See the [Privacy Policy](PrivacyPolicy.md).

## License

This project is licensed under the [MIT License](LICENSE).

## Thanks

To save maintenance costs, this project refers to stream resolving approaches and string data from [DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) and related resolver projects.
