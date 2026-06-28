using System.Windows.Forms;

namespace MPLR.Core;

internal static class LowBatteryProtection
{
    private const float LowThreshold = 0.20f;
    private const float SafeThreshold = 0.35f;
    private const string RecordBlockReason = "low-battery";
    private static PeriodicTimer? timer;
    private static CancellationTokenSource? tokenSource;
    private static readonly object SyncRoot = new();
    private static bool lowBatteryTriggered;
    private static bool processingQueue;

    public static bool IsLowBatteryActive { get; private set; }

    public static void Start()
    {
        lock (SyncRoot)
        {
            if (timer != null)
            {
                return;
            }

            tokenSource = new CancellationTokenSource();
            timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            _ = Task.Run(() => RunAsync(tokenSource.Token));
        }
    }

    public static void Stop()
    {
        lock (SyncRoot)
        {
            tokenSource?.Cancel();
            tokenSource = null;
            timer?.Dispose();
            timer = null;
            IsLowBatteryActive = false;
            GlobalMonitor.SetRecordStartBlock(RecordBlockReason, false);
        }
    }

    public static bool ShouldDeferTranscode()
    {
        return IsLowBatteryActive;
    }

    private static async Task RunAsync(CancellationToken token)
    {
        try
        {
            await CheckAsync(token);

            while (timer != null && await timer.WaitForNextTickAsync(token))
            {
                await CheckAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task CheckAsync(CancellationToken token)
    {
        PowerStatus status = SystemInformation.PowerStatus;
        bool onBattery = status.PowerLineStatus != PowerLineStatus.Online;
        float level = status.BatteryLifePercent;
        bool low = onBattery && level >= 0 && level <= LowThreshold;

        IsLowBatteryActive = low;

        if (low && !lowBatteryTriggered)
        {
            lowBatteryTriggered = true;
            GlobalMonitor.SetRecordStartBlock(RecordBlockReason, true);
            GlobalMonitor.StopAllRecorders();
            Toast.Warning("当前电量过低，已停止录制以保护录制文件");

            if (HasConfiguredTranscode())
            {
                Toast.Warning("当前电量过低，转码将在下一次有足够电量时进行");
            }
        }

        bool safe = !onBattery || level < 0 || level >= SafeThreshold;
        if (safe)
        {
            lowBatteryTriggered = false;
            IsLowBatteryActive = false;
            GlobalMonitor.SetRecordStartBlock(RecordBlockReason, false);

            if (!processingQueue)
            {
                processingQueue = true;
                try
                {
                    await PendingTranscodeQueue.ProcessAsync(token);
                }
                finally
                {
                    processingQueue = false;
                }
            }
        }
    }

    private static bool HasConfiguredTranscode()
    {
        if (Configurations.RecordFormat.Get().Contains("->", StringComparison.Ordinal))
        {
            return true;
        }

        return Configurations.Rooms.Get().Any(room =>
            !room.IsFollowGlobalSettings &&
            !string.IsNullOrWhiteSpace(room.RecordFormat) &&
            room.RecordFormat.Contains("->", StringComparison.Ordinal));
    }
}
