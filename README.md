# MPLR

MPLR，即 MultiPlatformLiveRecorder，是一款面向多平台直播录制场景的项目。它的目标是统一管理直播间地址、录制任务、输出文件和运行日志，让直播录制流程更稳定、更容易维护。

## 项目目标

- 支持多个直播平台的录制入口
- 支持直播间状态检测和自动开录
- 支持录制文件按平台、主播、日期归档
- 支持断线重试、任务恢复和日志追踪
- 支持本地配置管理，避免敏感信息进入仓库

## 当前状态

项目已完成 Git 初始化，并补充了基础 `.gitignore`、`README.md` 和 Apache License。后续可以继续添加实际录制逻辑、配置模板、命令行入口或桌面界面。

## 建议目录结构

```text
MPLR/
├── src/
├── config/
├── scripts/
├── tests/
├── recordings/
└── README.md
```

## 本地开发

克隆仓库后，根据后续实际技术栈安装依赖并启动项目。

```bash
git clone git@github.com:qzj1472/MPLR.git
cd MPLR
```

## 配置说明

建议将本地配置放在 `.env` 或 `config/local.*` 中，并提供不包含敏感信息的示例文件，例如 `.env.example`。

## 录制输出

默认建议将录制结果放在 `recordings/`、`downloads/`、`captures/` 或 `output/` 目录中。这些目录已加入 `.gitignore`，避免误提交大文件。

## License

本项目使用 Apache License 2.0，详见 `LICENSE`。
