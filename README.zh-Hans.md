[English](README.md) | 简体中文

<img src="src/MPLR/Assets/Favicon.png" alt="MPLR icon" width="96" />

# MPLR

MPLR 是一款仅支持 Windows 的 WPF 桌面端直播录制工具，主要面向直播间监控、直播预览和无人值守录制。项目继承自 [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)，内置的 Python 解析器参考了 [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) 的维护思路。

## 主要功能

- 统一管理直播间链接。
- 按直播间单独控制监控、录制和通知。
- 支持预览后录制，也支持无人值守后台录制。
- 内置 FFmpeg 和 FFprobe，用于录制与媒体探测。
- 需要时通过内置 Python 解析器获取真实流地址。
- 配置、日志和缓存默认保存在程序目录下。
- 支持平台 Cookie 配置，便于应对需要鉴权的平台。

## 支持平台

- 抖音
- TikTok
- 斗鱼
- 虎牙
- 快手
- B 站
- Twitch
- 小红书

## 运行环境

- Windows
- .NET Desktop Runtime 9.0
- Python 3.11 或更高版本

## 安装

GitHub Releases 每个通道只提供一个面向用户的安装器：

- 正式版：`MPLR-win.msi`
- Beta 版：`MPLR-win-beta.msi`

MSI 安装器支持选择安装位置。软件内自动更新会继续更新当前安装目录，不会另行安装到默认目录导致出现两份。安装器默认不会开启开机自启动；开机自启动由用户在 MPLR 托盘菜单中手动开启或关闭。

## 构建

```powershell
dotnet build .\src\MPLR\MPLR.csproj -c Debug -p:Platform=x64
```

## 启动

```powershell
.\src\MPLR\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\MPLR.exe
```

## 打包

GitHub Actions 会构建并发布 Velopack MSI 安装器和自动更新资产。旧的本地脚本仍可用于生成手动归档包：

```powershell
.\build\publish_win-x64.cmd
```

## 项目结构

```text
src/MPLR              主 WPF 应用
docs                  Cookie、隐私政策与发布日志文档
build                 发布脚本、安装器文案和打包资源
tools/stream_resolver 内置 Python 解析器及依赖
tools/ffmpeg/win-x64  FFmpeg 和 FFprobe 二进制文件
```

## 文档

- [抖音 Cookie 获取](docs/GETCOOKIE_DOUYIN.md)
- [TikTok Cookie 获取](docs/GETCOOKIE_TIKTOK.md)
- [隐私政策](docs/PrivacyPolicy.zh-Hans.md)

## 致谢

- [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)
- [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder)

## 许可

项目基于 [MIT License](LICENSE) 发布。
