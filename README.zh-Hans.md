[English](README.md) | [简体中文](README.zh-Hans.md)

<img src="branding/logo.png" />

# 多平台录播

[![GitHub license](https://img.shields.io/github/license/emako/TiktokLiveRec)](https://github.com/emako/TiktokLiveRec/blob/master/LICENSE) [![Actions](https://github.com/emako/TiktokLiveRec/actions/workflows/build.yml/badge.svg)](https://github.com/emako/TiktokLiveRec/actions/workflows/library.nuget.yml) [![Platform](https://img.shields.io/badge/platform-Windows-blue?logo=windowsxp&color=1E9BFA)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) [![GitHub downloads](https://img.shields.io/github/downloads/emako/TiktokLiveRec/total)](https://github.com/emako/TiktokLiveRec/releases)
[![GitHub downloads](https://img.shields.io/github/downloads/emako/TiktokLiveRec/latest/total)](https://github.com/emako/TiktokLiveRec/releases)

具有图形界面、无人值守、直播预览和直播流录制功能。

实现基于 FFmpeg 和 FFplay。

## 截图

<img src="assets/image-20241113165448238.png" alt="image-20241113165448238" style="transform:scale(0.5);" />

## 依赖运行时

Windows: [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

其他系统: [.NET Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

Windows 本地开发可使用 Scoop 安装环境：

```powershell
scoop bucket add versions
scoop bucket add extras
scoop install versions/dotnet9-sdk ffmpeg extras/vcredist2022 python311 nodejs
```

## 直播录制

内置 C# 回退解析支持抖音和 TikTok。随包提供的 Python 直播流解析器扩展了更多平台的录制能力。

| 平台 | 状态 |
| ---- | ---- |
| 抖音 | 支持 |
| TikTok | 支持 |
| 哔哩哔哩 | 通过直播流解析器支持 |
| 快手 | 通过直播流解析器支持 |
| 斗鱼 | 通过直播流解析器支持 |
| 虎牙 | 通过直播流解析器支持 |
| Twitch | 通过直播流解析器支持 |
| YouTube 及其他解析器支持的平台 | 解析器能返回直播流时支持 |

怎么添加直播间：

```bash
# 国内抖音直播间链接类似如下：
https://live.douyin.com/XXX
https://www.douyin.com/root/live/XXX

# 海外 TikTok 直播间链接类似如下：
https://www.tiktok.com/@XXX/live

# 其他平台通常可以直接添加公开直播间链接：
https://live.bilibili.com/XXX
https://www.douyu.com/XXX
https://www.huya.com/XXX
https://www.twitch.tv/XXX
```

## 支持系统

为了加快初版开发实现，首版基于 WPF 开发了 Windows 版本。

其他系统的实现会基于个人需求和用户反馈推进。

| 操作系统 | 开发框架 | 状态 |
| -------- | -------- | ---- |
| Windows | WPF | 支持 |
| macOS | Avalonia | 开发中 |
| Ubuntu | Avalonia | 待开发 |
| Android | Avalonia | 待开发 |
| iOS | Avalonia | 待开发 |
| tvOS | 待定 | 待开发 |

## Cookie

部分平台可能需要 Cookie、登录状态或代理才能稳定解析直播流。可以参考 [GETCOOKIE_DOUYIN.md](doc/GETCOOKIE_DOUYIN.md) 或 [GETCOOKIE_TIKTOK.md](doc/GETCOOKIE_TIKTOK.md)。

## 隐私政策

[查看隐私政策](PrivacyPolicy.zh-Hans.md)。

## 许可证

本项目基于 [MIT 许可证](LICENSE)。

## 鸣谢

为了节约后续维护成本，本项目参考了 [DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) 以及相关解析器项目的直播流解析方式和字符串数据。
