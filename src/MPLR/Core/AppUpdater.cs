using System.Windows;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace MPLR.Core;

internal static class AppUpdater
{
    private const string RepositoryUrl = "https://github.com/qzj1472/MPLR";
    private static readonly SemaphoreSlim CheckLock = new(1, 1);
    private static bool backgroundCheckStarted;

    private enum UpdateChannelPreference
    {
        Auto,
        Stable,
        Beta,
    }

    public static bool IsForcedUpdateEnabled => AppConfig.IsBetaBuild || GetUpdateChannelPreference() == UpdateChannelPreference.Beta;

    public static void CheckInBackground()
    {
        if (backgroundCheckStarted)
        {
            return;
        }

        backgroundCheckStarted = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(8));
            await CheckAsync(showNoUpdateMessage: false);
        });
    }

    public static Task CheckAsync(bool showNoUpdateMessage)
    {
        return CheckAsync(showNoUpdateMessage, forceInstall: false);
    }

    public static async Task CheckAsync(bool showNoUpdateMessage, bool forceInstall)
    {
        if (!await CheckLock.WaitAsync(0))
        {
            if (showNoUpdateMessage)
            {
                await ShowToastAsync(() => Toast.Information("正在检查更新，请稍候"));
            }

            return;
        }

        try
        {
            await CheckCoreAsync(showNoUpdateMessage, forceInstall);
        }
        finally
        {
            CheckLock.Release();
        }
    }

    private static async Task CheckCoreAsync(bool showNoUpdateMessage, bool forceInstall)
    {
        try
        {
            UpdateManager manager = CreateManager();
            string channelName = GetChannelDisplayName();
            bool forced = forceInstall || IsForcedUpdateEnabled;
            VelopackAsset? pendingRestart = manager.UpdatePendingRestart;

            if (pendingRestart != null)
            {
                await AskApplyPendingUpdateAsync(manager, pendingRestart, forced);
                return;
            }

            if (showNoUpdateMessage)
            {
                await ShowToastAsync(() => Toast.Information($"正在检查{channelName}更新"));
            }

            UpdateInfo? update = await manager.CheckForUpdatesAsync();
            if (update == null)
            {
                if (showNoUpdateMessage)
                {
                    await ShowToastAsync(() => Toast.Success($"当前已是{channelName}最新版本"));
                }

                return;
            }

            await AskDownloadUpdateAsync(manager, update, forced);
        }
        catch (NotInstalledException)
        {
            if (showNoUpdateMessage)
            {
                await ShowMessageAsync("当前不是 Velopack 安装版，无法自动更新。请从 GitHub Releases 下载新版安装包。");
            }
        }
        catch (Exception exception)
        {
            AppSessionLogger.WriteException(exception);

            if (showNoUpdateMessage)
            {
                await ShowToastAsync(() => Toast.Error($"检查更新失败：{exception.Message}"));
            }
        }
    }

    private static UpdateManager CreateManager()
    {
        UpdateChannelPreference preference = GetUpdateChannelPreference();
        bool includePrereleases = ShouldIncludePrereleases(preference);
        string? explicitChannel = preference switch
        {
            UpdateChannelPreference.Stable => AppConfig.StableUpdateChannel,
            UpdateChannelPreference.Beta => AppConfig.BetaUpdateChannel,
            _ => null,
        };
        UpdateOptions options = new()
        {
            AllowVersionDowngrade = explicitChannel != null,
            ExplicitChannel = explicitChannel,
        };

        return new UpdateManager(new GithubSource(RepositoryUrl, null, includePrereleases, null), options, null);
    }

    private static async Task AskApplyPendingUpdateAsync(UpdateManager manager, VelopackAsset pendingRestart, bool forced)
    {
        string version = GetVersionText(pendingRestart);
        if (forced)
        {
            await ShowMessageAsync($"{GetChannelDisplayName()}更新 {version} 已下载完成，将重启并完成安装。");
            manager.ApplyUpdatesAndRestart(pendingRestart);
            return;
        }

        MessageBoxResult result = await ShowQuestionAsync($"新版本 {version} 已下载完成，是否立即重启并完成安装？");

        if (result == MessageBoxResult.Yes)
        {
            manager.ApplyUpdatesAndRestart(pendingRestart);
        }
    }

    private static async Task AskDownloadUpdateAsync(UpdateManager manager, UpdateInfo update, bool forced)
    {
        string version = GetVersionText(update.TargetFullRelease);
        string channelName = GetChannelDisplayName();
        if (forced)
        {
            await ShowMessageAsync($"发现{channelName}新版本 {version}，将自动下载并安装。");
        }
        else
        {
            MessageBoxResult result = await ShowQuestionAsync($"发现{channelName}新版本 {version}，是否立即下载？下载完成后可重启安装。");

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await ShowToastAsync(() => Toast.Information($"正在下载 {version}"));
        int lastProgress = -1;
        await manager.DownloadUpdatesAsync(update, progress =>
        {
            if (progress < 100 && progress < lastProgress + 20)
            {
                return;
            }

            lastProgress = progress;
            AppSessionLogger.Event("info", "update", "download_progress", $"{progress}%", new
            {
                version,
                progress,
            });
        }, CancellationToken.None);

        if (forced)
        {
            await ShowMessageAsync($"{channelName}新版本 {version} 已下载完成，将重启并完成安装。");
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
            return;
        }

        MessageBoxResult restartResult = await ShowQuestionAsync($"新版本 {version} 已下载完成，是否立即重启并完成安装？");

        if (restartResult == MessageBoxResult.Yes)
        {
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
        }
        else
        {
            await ShowToastAsync(() => Toast.Success("更新已下载，可稍后在关于里再次检查并安装"));
        }
    }

    private static string GetVersionText(VelopackAsset asset)
    {
        string value = asset.Version?.ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ? "未知版本" : $"v{value}";
    }

    private static bool ShouldIncludePrereleases(UpdateChannelPreference preference)
    {
        return preference == UpdateChannelPreference.Beta ||
               (preference == UpdateChannelPreference.Auto && AppConfig.IsBetaBuild);
    }

    private static UpdateChannelPreference GetUpdateChannelPreference()
    {
        return (Configurations.UpdateChannel.Get() ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "stable" => UpdateChannelPreference.Stable,
            "beta" => UpdateChannelPreference.Beta,
            _ => UpdateChannelPreference.Auto,
        };
    }

    private static string GetChannelDisplayName()
    {
        return GetUpdateChannelPreference() switch
        {
            UpdateChannelPreference.Stable => "稳定版",
            UpdateChannelPreference.Beta => "Beta 版",
            _ => AppConfig.IsBetaBuild ? "Beta 版" : "稳定版",
        };
    }

    private static Task ShowToastAsync(Action action)
    {
        Application? app = Application.Current;

        if (app == null || app.Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return app.Dispatcher.InvokeAsync(action).Task;
    }

    private static Task ShowMessageAsync(string message)
    {
        Application? app = Application.Current;

        if (app == null || app.Dispatcher.CheckAccess())
        {
            return MessageBox.InformationAsync(message);
        }

        return app.Dispatcher.InvokeAsync(() => MessageBox.InformationAsync(message)).Task.Unwrap();
    }

    private static Task<MessageBoxResult> ShowQuestionAsync(string message)
    {
        Application? app = Application.Current;

        if (app == null || app.Dispatcher.CheckAccess())
        {
            return MessageBox.QuestionAsync(message);
        }

        return app.Dispatcher.InvokeAsync(() => MessageBox.QuestionAsync(message)).Task.Unwrap();
    }
}
