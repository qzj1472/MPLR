[English](README.md) | 简体中文

<img src="src/TiktokLiveRec.WPF/Assets/Favicon.png" alt="MPLR icon" width="96" />

# MPLR 多平台录播

MPLR 是一款带图形界面的多平台直播录制工具，支持直播间管理、后台监控、开播通知、直播预览和基于 FFmpeg 的无人值守录制。本项目基于 [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec) 继续整理和扩展，并集成了参考 [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) 与 [wbt5/real-url](https://github.com/wbt5/real-url) 的 Python 直播流解析器。

## 功能

- Windows WPF 桌面端，当前唯一支持入口。
- Avalonia 源码入口仅作为实验原型保留，不作为当前支持平台。
- 直播间列表管理，支持每个直播间单独控制监控、录制和通知。
- 后台轮询直播状态，开播后自动录制。
- 使用 FFplay 或自定义播放器预览直播。
- 使用 FFmpeg 录制直播流，支持代理、自定义 User-Agent、分段录制和录制后转封装。
- 内置抖音和 TikTok 的 C# 回退解析逻辑。
- 集成 Python 解析器，扩展哔哩哔哩、斗鱼、虎牙、快手、Twitch、YouTube 等平台的直播流解析能力。
- 支持 Cookie、平台 Cookie、代理和清晰度配置，应对登录态、风控和地区访问限制。

## 环境依赖

运行环境：

- Windows: [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Python 3.11 或更新版本，供内置直播流解析器使用。
- FFmpeg 和 FFplay，供录制和预览使用。Windows 项目已在 `tools/ffmpeg/win-x64` 下包含打包用二进制文件。

Windows 本地开发可使用 Scoop 安装环境：

```powershell
scoop bucket add versions
scoop bucket add extras
scoop install versions/dotnet9-sdk ffmpeg extras/vcredist2022 python311 nodejs
```

## 构建和运行

构建 Windows WPF 主程序：

```powershell
dotnet build .\src\TiktokLiveRec.WPF\TiktokLiveRec.WPF.csproj -c Debug -p:Platform=x64
```

启动构建产物：

```powershell
.\src\TiktokLiveRec.WPF\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\MultiPlatformLiveRecorder.exe
```

Avalonia 项目可从源码启动用于开发实验，但它不是当前支持的发布目标：

```powershell
dotnet run --project .\src\TiktokLiveRec.Avalonia\TiktokLiveRec.Avalonia.csproj
```

## 添加直播间

可以添加公开直播间地址，例如：

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

部分平台可能需要 Cookie、登录态、代理或更长的轮询间隔才能稳定解析。抖音和 TikTok 的 Cookie 获取方式可参考 [GETCOOKIE_DOUYIN.md](docs/GETCOOKIE_DOUYIN.md) 与 [GETCOOKIE_TIKTOK.md](docs/GETCOOKIE_TIKTOK.md)。

## 项目结构

```text
src/TiktokLiveRec.WPF             Windows WPF 主程序
src/TiktokLiveRec.Avalonia        Avalonia 桌面端原型
src/FluentAvaloniaUI.Violeta      本地 Avalonia UI 控件
docs                             用户文档和文档图片
build                            发布脚本和打包资源
build/assets                     品牌、安装器和包资源
tools/stream_resolver            Python 直播流解析器和 vendored 解析代码
tools/ffmpeg/win-x64             Windows FFmpeg、FFplay、FFprobe
```

## 工作流程

1. 桌面端负责保存直播间、配置和操作状态。
2. `GlobalMonitor` 按轮询间隔刷新每个直播间状态。
3. `Spider` 优先调用内置 Python 解析器；解析失败时，抖音和 TikTok 会回退到 C# 内置解析。
4. `Recorder` 从 FLV、HLS 或直接录制地址中选择可用输入，然后启动 FFmpeg。
5. FFmpeg 将直播流保存为 FLV 或 TS，可按时间分段，也可在录制结束后转封装。

## 平台支持

当前明确只支持 Windows。

| 系统 | 框架 | 状态 |
| --- | --- | --- |
| Windows | WPF | 支持 |
| macOS | Avalonia | 不支持 |
| Linux | Avalonia | 不支持 |
| Android / iOS / tvOS | Avalonia 或原生 | 不支持 |

## 上游和致谢

本项目明确引用和感谢以下项目：

- [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)：原始 GUI 录播项目，也是本项目的基础来源。
- [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder)：基于 FFmpeg 的多平台循环值守录制工具，本项目的 Python 解析器和部分解析思路参考了该项目。
- [wbt5/real-url](https://github.com/wbt5/real-url)：多平台真实直播源和弹幕解析项目，本项目 `tools/stream_resolver/vendor/real_url` 中保留了相关解析模块和原始许可。

## 隐私政策

[查看隐私政策](docs/PrivacyPolicy.zh-Hans.md)。

## 许可证

主项目基于 [MIT 许可证](LICENSE) 发布。

仓库内 vendored 第三方代码保留各自原始许可。其中 `tools/stream_resolver/vendor/real_url` 保留来自 `wbt5/real-url` 的 GPL-2.0 许可，`tools/stream_resolver/vendor/douyin_live_recorder` 保留来自 `ihmily/DouyinLiveRecorder` 的 MIT 许可。
