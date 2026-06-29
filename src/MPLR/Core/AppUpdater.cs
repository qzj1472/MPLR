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

    public static async Task CheckAsync(bool showNoUpdateMessage)
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
            await CheckCoreAsync(showNoUpdateMessage);
        }
        finally
        {
            CheckLock.Release();
        }
    }

    private static async Task CheckCoreAsync(bool showNoUpdateMessage)
    {
        try
        {
            UpdateManager manager = CreateManager();
            VelopackAsset? pendingRestart = manager.UpdatePendingRestart;

            if (pendingRestart != null)
            {
                await AskApplyPendingUpdateAsync(manager, pendingRestart);
                return;
            }

            if (showNoUpdateMessage)
            {
                await ShowToastAsync(() => Toast.Information("正在检查更新"));
            }

            UpdateInfo? update = await manager.CheckForUpdatesAsync();
            if (update == null)
            {
                if (showNoUpdateMessage)
                {
                    await ShowToastAsync(() => Toast.Success("当前已是最新版本"));
                }

                return;
            }

            await AskDownloadUpdateAsync(manager, update);
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
        return new UpdateManager(new GithubSource(RepositoryUrl, null, prerelease: false, null));
    }

    private static async Task AskApplyPendingUpdateAsync(UpdateManager manager, VelopackAsset pendingRestart)
    {
        string version = GetVersionText(pendingRestart);
        MessageBoxResult result = await ShowQuestionAsync($"新版本 {version} 已下载完成，是否立即重启并完成安装？");

        if (result == MessageBoxResult.Yes)
        {
            manager.ApplyUpdatesAndRestart(pendingRestart);
        }
    }

    private static async Task AskDownloadUpdateAsync(UpdateManager manager, UpdateInfo update)
    {
        string version = GetVersionText(update.TargetFullRelease);
        MessageBoxResult result = await ShowQuestionAsync($"发现新版本 {version}，是否立即下载？下载完成后可重启安装。");

        if (result != MessageBoxResult.Yes)
        {
            return;
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
