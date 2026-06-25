[English](README.md) | 简体中文

<img src="src/MPLR/Assets/Favicon.png" alt="MPLR icon" width="96" />

# MPLR

MPLR 是当前的 Windows 桌面版直播录制工具，聚焦直播间巡检、预览和无人值守 FFmpeg 录制。当前仅保留 WPF 单入口，项目延续自 [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)，内置的 Python 解析器参考并跟进 [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) 的维护方案。

## 功能

- 仅保留 Windows WPF 入口。
- 直播间列表管理，支持单独控制巡检、录制和通知。
- 自动轮询、自动录制、直播预览。
- 内置 FFmpeg、FFprobe、FFplay 便携打包。
- 配置、日志、缓存全部落在软件根目录下的 `config`、`logs`、`cache`。
- 支持实时会话日志、用户操作日志、配置变更日志。
- 预览时自动补充 `User-Agent`、`Referer` 及相关请求头。
- 内置抖音、TikTok 回退解析，并集成 Python 多平台解析器。

## 运行环境

- Windows
- [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Python 3.11 或更新版本，用于内置解析器

本地开发可选安装：

```powershell
scoop bucket add versions
scoop bucket add extras
scoop install versions/dotnet9-sdk ffmpeg extras/vcredist2022 python311 nodejs
```

## 构建

```powershell
dotnet build .\src\MPLR\MPLR.csproj -c Debug -p:Platform=x64
```

运行：

```powershell
.\src\MPLR\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\MPLR.exe
```

## 项目结构

```text
src/MPLR                    Windows WPF 主程序
docs                        用户文档
build                       打包脚本和资源
tools/stream_resolver       内置 Python 解析器
tools/ffmpeg/win-x64        FFmpeg、FFprobe、FFplay
```

## 致谢

- [qzj1472/MPLR](https://github.com/qzj1472/MPLR)
- [emako/TiktokLiveRec](https://github.com/emako/TiktokLiveRec)
- [ihmily/DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder)

## 许可

项目主体以 [MIT License](LICENSE) 发布。
