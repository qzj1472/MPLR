using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fischless.Configuration;
using MediaInfoLib;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Web;
using MPLR.Models;
using MPLR.Threading;
using Windows.System;
using Wpf.Ui.Violeta.Resources;

namespace MPLR.Core;

internal static class GlobalMonitor
{
    /// <summary>
    /// ConcurrentDictionary{RoomUrl: string, RoomStatus: RoomStatus>}
    /// </summary>
    public static ConcurrentDictionary<string, RoomStatus> RoomStatus { get; } = new();

    public static PeriodicWait RoutinePeriodicWait = new(TimeSpan.FromMilliseconds(int.Max(Configurations.RoutineInterval.Get(), 500)), TimeSpan.Zero);

    public static CancellationTokenSource? TokenSource { get; private set; } = null;

    private static readonly object MonitorLock = new();

    private static Task? MonitorTask = null;

    private static readonly ConcurrentDictionary<string, bool> TemporaryRoomMonitorOverrides = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, bool> TemporaryRoomRecordOverrides = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RoomCheckLocks = new(StringComparer.OrdinalIgnoreCase);

    private sealed class GlobalMonitorRecipient : ObservableRecipient
    {
        public static GlobalMonitorRecipient Instance { get; } = new();
    }

    static GlobalMonitor()
    {
        WeakReferenceMessenger.Default.Register<ToastNotificationActivatedMessage>(GlobalMonitorRecipient.Instance, async (_, msg) =>
        {
            string arguments = msg.EventArgs.Argument;

            if (!string.IsNullOrEmpty(arguments))
            {
                NameValueCollection parsedArgs = HttpUtility.ParseQueryString(arguments);

                if (parsedArgs["RoomUrl"] != null)
                {
                    try
                    {
                        // TODO: Implement for other platforms
                        await Launcher.LaunchUriAsync(new Uri(parsedArgs["RoomUrl"]!));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
                else if (parsedArgs["OffRemindTheCloseToTrayHint"] != null)
                {
                    try
                    {
                        Configurations.IsOffRemindCloseToTray.Set(true);
                        ConfigurationManager.Save();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
            }
        });
    }

    public static void Start(CancellationTokenSource? tokenSource = null)
    {
        lock (MonitorLock)
        {
            if (TokenSource != null && !TokenSource.IsCancellationRequested && MonitorTask is { IsCompleted: false })
            {
                return;
            }

            CancellationTokenSource source = tokenSource ?? new CancellationTokenSource();
            TokenSource = source;
            RoutinePeriodicWait = CreateRoutinePeriodicWait();

            MonitorTask = Task.Factory.StartNew(
                () => StartAsync(source.Token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            ).Unwrap();
        }
    }

    public static void Stop()
    {
        lock (MonitorLock)
        {
            TokenSource?.Cancel();
            TokenSource = null;
        }
    }

    public static void StopAllRecorders()
    {
        foreach (RoomStatus roomStatus in RoomStatus.Values)
        {
            roomStatus.Recorder.Stop();
        }
    }

    public static bool GetEffectiveRoomNotify(Room room)
    {
        return Configurations.IsToNotify.Get() && room.IsToNotify;
    }

    public static bool GetEffectiveRoomRecord(Room room)
    {
        return GetEffectiveRoomRecord(room.RoomUrl, room.IsToRecord, room.IsFollowGlobalSettings);
    }

    public static bool GetEffectiveRoomMonitor(Room room)
    {
        return GetEffectiveRoomMonitor(room.RoomUrl, room.IsToMonitor, room.IsFollowGlobalSettings);
    }

    public static bool GetEffectiveRoomRecord(string roomUrl, bool roomValue, bool followsGlobal)
    {
        bool value = followsGlobal ? Configurations.IsToRecord.Get() : roomValue;
        return TemporaryRoomRecordOverrides.TryGetValue(roomUrl, out bool temporaryValue) ? temporaryValue : value;
    }

    public static bool GetEffectiveRoomMonitor(string roomUrl, bool roomValue, bool followsGlobal)
    {
        bool value = followsGlobal ? Configurations.IsMonitorRunning.Get() : roomValue;
        return TemporaryRoomMonitorOverrides.TryGetValue(roomUrl, out bool temporaryValue) ? temporaryValue : value;
    }

    public static void SetTemporaryRoomRecord(string roomUrl, bool enabled)
    {
        if (!string.IsNullOrWhiteSpace(roomUrl))
        {
            TemporaryRoomRecordOverrides[roomUrl] = enabled;
        }
    }

    public static void ClearTemporaryRoomRecord(string roomUrl)
    {
        if (!string.IsNullOrWhiteSpace(roomUrl))
        {
            _ = TemporaryRoomRecordOverrides.TryRemove(roomUrl, out _);
        }
    }

    public static void ClearTemporaryRecordOverrides()
    {
        TemporaryRoomRecordOverrides.Clear();
    }

    public static void SetTemporaryRoomMonitor(string roomUrl, bool enabled)
    {
        if (!string.IsNullOrWhiteSpace(roomUrl))
        {
            TemporaryRoomMonitorOverrides[roomUrl] = enabled;
        }
    }

    public static void ClearTemporaryRoomOverrides(string roomUrl)
    {
        if (!string.IsNullOrWhiteSpace(roomUrl))
        {
            _ = TemporaryRoomRecordOverrides.TryRemove(roomUrl, out _);
            _ = TemporaryRoomMonitorOverrides.TryRemove(roomUrl, out _);
        }
    }

    private static PeriodicWait CreateRoutinePeriodicWait()
    {
        return new PeriodicWait(TimeSpan.FromMilliseconds(int.Max(Configurations.RoutineInterval.Get(), 500)), TimeSpan.Zero);
    }

    public static async Task RunOnceAsync(CancellationToken token = default)
    {
        await RunRoomsAsync(Configurations.Rooms.Get(), token);
    }

    public static async Task RunRoomAsync(string roomUrl, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(roomUrl))
        {
            return;
        }

        Room[] rooms = Configurations.Rooms.Get()
            .Where(room => string.Equals(room.RoomUrl, roomUrl, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        await RunRoomsAsync(rooms, token);
    }

    public static async Task StartAsync(CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            if (!await RoutinePeriodicWait.WaitForNextTickAsync(token))
            {
                break;
            }

            if (!RoutineScheduleHelper.IsActive(DateTime.Now))
            {
                continue;
            }

            await RunOnceAsync(token);
        }
    }

    private static async Task RunRoomsAsync(IEnumerable<Room> rooms, CancellationToken token = default)
    {
        try
        {
            bool isGlobalToNotify = Configurations.IsToNotify.Get();
            bool isGlobalToRecord = Configurations.IsToRecord.Get();
            bool isGlobalMonitorRunning = Configurations.IsMonitorRunning.Get();

            using SemaphoreSlim semaphore = new(Math.Clamp(Environment.ProcessorCount, 4, 8));
            List<Task> tasks = [];

            foreach (Room room in DistinctRoomsByUrl(rooms))
            {
                token.ThrowIfCancellationRequested();

                if (TryGetRoomStatus(room) is not RoomStatus roomStatus)
                {
                    continue;
                }

                bool shouldNotify = isGlobalToNotify && room.IsToNotify;
                bool shouldRecord = room.IsFollowGlobalSettings ? isGlobalToRecord : room.IsToRecord;
                bool shouldMonitor = room.IsFollowGlobalSettings ? isGlobalMonitorRunning : room.IsToMonitor;
                shouldRecord = TemporaryRoomRecordOverrides.TryGetValue(room.RoomUrl, out bool recordOverride) ? recordOverride : shouldRecord;
                shouldMonitor = TemporaryRoomMonitorOverrides.TryGetValue(room.RoomUrl, out bool monitorOverride) ? monitorOverride : shouldMonitor;

                if (shouldMonitor)
                {
                    tasks.Add(RunRoomCheckWithSemaphoreAsync(semaphore, room, roomStatus, shouldNotify, shouldRecord, token));
                }
                else
                {
                    if (roomStatus.RecordStatus != RecordStatus.Recording)
                    {
                        roomStatus.RecordStatus = RecordStatus.Disabled;
                    }
                    roomStatus.StreamStatus = StreamStatus.Disabled;
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private static async Task RunRoomCheckWithSemaphoreAsync(SemaphoreSlim semaphore, Room room, RoomStatus roomStatus, bool shouldNotify, bool shouldRecord, CancellationToken token)
    {
        await semaphore.WaitAsync(token);
        SemaphoreSlim roomLock = RoomCheckLocks.GetOrAdd(room.RoomUrl, _ => new SemaphoreSlim(1, 1));
        bool roomLockTaken = false;

        try
        {
            await roomLock.WaitAsync(token);
            roomLockTaken = true;
            await RunRoomCheckAsync(room, roomStatus, shouldNotify, shouldRecord, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            AppSessionLogger.WriteException(e);
        }
        finally
        {
            if (roomLockTaken)
            {
                roomLock.Release();
            }
            semaphore.Release();
        }
    }

    private static async Task RunRoomCheckAsync(Room room, RoomStatus roomStatus, bool shouldNotify, bool shouldRecord, CancellationToken token)
    {
        ISpiderResult? spiderResult = await Task.Run(() => Spider.GetResult(room.RoomUrl), token);
        shouldRecord = GetEffectiveRoomRecord(room);

        if (!GetEffectiveRoomMonitor(room))
        {
            if (roomStatus.RecordStatus == RecordStatus.Recording)
            {
                roomStatus.Recorder.Stop();
            }

            roomStatus.RecordStatus = RecordStatus.Disabled;
            roomStatus.StreamStatus = StreamStatus.Disabled;
            return;
        }

        if (spiderResult == null)
        {
            roomStatus.Platform = PlatformDetector.DetectFromUrl(room.RoomUrl);

            if (!shouldRecord)
            {
                if (roomStatus.RecordStatus == RecordStatus.Recording)
                {
                    AppSessionLogger.Event("info", "business", "record_stop_requested", "record stop requested because recording is disabled", new
                    {
                        room.RoomUrl,
                        room.NickName,
                        roomStatus.Platform,
                        roomStatus.RecordStatus,
                    });
                    roomStatus.Recorder.Stop();
                }

                roomStatus.RecordStatus = RecordStatus.Disabled;
                roomStatus.StreamStatus = StreamStatus.Disabled;
                return;
            }

            if (roomStatus.RecordStatus == RecordStatus.Recording)
            {
                AppSessionLogger.Event("warn", "business", "room_check_failed_while_recording", "room check failed while recorder is still running", new
                {
                    room.RoomUrl,
                    room.NickName,
                    roomStatus.Platform,
                    hasRecordUrl = !string.IsNullOrWhiteSpace(roomStatus.RecordUrl),
                    hasFlvUrl = !string.IsNullOrWhiteSpace(roomStatus.FlvUrl),
                    hasHlsUrl = !string.IsNullOrWhiteSpace(roomStatus.HlsUrl),
                });

                if (HasRecordableStream(roomStatus))
                {
                    roomStatus.StreamStatus = StreamStatus.Streaming;
                }

                return;
            }

            if (roomStatus.RecordStatus != RecordStatus.Recording)
            {
                roomStatus.RecordStatus = RecordStatus.NotRecording;
            }

            roomStatus.StreamStatus = StreamStatus.NotStreaming;
            return;
        }

        StreamStatus prevStreamStatus = roomStatus.StreamStatus;

        if (!string.IsNullOrWhiteSpace(spiderResult.AvatarThumbUrl))
        {
            roomStatus.AvatarThumbUrl = spiderResult.AvatarThumbUrl;
            roomStatus.AvatarLocalPath = await AvatarCache.UpdateAsync(room.RoomUrl, spiderResult.AvatarThumbUrl, token);
        }
        bool hasFreshRecordableStream = HasRecordableStream(spiderResult);
        if (hasFreshRecordableStream || roomStatus.RecordStatus != RecordStatus.Recording)
        {
            roomStatus.FlvUrl = spiderResult.FlvUrl ?? string.Empty;
            roomStatus.HlsUrl = spiderResult.HlsUrl ?? string.Empty;
            roomStatus.RecordUrl = spiderResult.RecordUrl ?? string.Empty;
        }

        roomStatus.Platform = string.IsNullOrWhiteSpace(spiderResult.Platform)
            ? PlatformDetector.DetectFromUrl(room.RoomUrl)
            : spiderResult.Platform;

        if (!string.IsNullOrWhiteSpace(spiderResult.Title))
        {
            roomStatus.Title = spiderResult.Title;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Uid))
        {
            roomStatus.Uid = spiderResult.Uid;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Quality))
        {
            roomStatus.Quality = spiderResult.Quality;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Headers))
        {
            roomStatus.Headers = spiderResult.Headers;
        }

        if (IsUsableResolution(spiderResult.Resolution))
        {
            roomStatus.Resolution = spiderResult.Resolution!;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Bitrate))
        {
            roomStatus.Bitrate = spiderResult.Bitrate!;
        }

        lock (MonitorLock)
        {
            Room[] rooms = Configurations.Rooms.Get();
            Room? cachedRoom = rooms.FirstOrDefault(item => string.Equals(item.RoomUrl, room.RoomUrl, StringComparison.OrdinalIgnoreCase));
            if (cachedRoom != null)
            {
                RoomInfoCache.Apply(roomStatus, cachedRoom);
                Configurations.Rooms.Set(rooms);
                ConfigurationManager.Save();
            }
        }

        bool isLiveStreaming = IsLiveStreaming(spiderResult);

        if (roomStatus.StreamStatus == StreamStatus.Streaming
            && roomStatus.RecordStatus == RecordStatus.Recording
            && (DateTime.Now - roomStatus.Recorder.StartTime).TotalSeconds < 30)
        {
        }
        else if (roomStatus.RecordStatus == RecordStatus.Recording && !isLiveStreaming && HasRecordableStream(roomStatus))
        {
            roomStatus.StreamStatus = StreamStatus.Streaming;
        }
        else
        {
            roomStatus.StreamStatus = spiderResult.IsLiveStreaming switch
            {
                true => StreamStatus.Streaming,
                false => StreamStatus.NotStreaming,
                null or _ => isLiveStreaming ? StreamStatus.Streaming : StreamStatus.NotStreaming,
            };
        }

        if (shouldRecord)
        {
            if (isLiveStreaming && HasRecordableStream(roomStatus))
            {
                if (roomStatus.RecordStatus != RecordStatus.Recording)
                {
                    AppSessionLogger.Event("info", "business", "record_start_requested", "record start requested", new
                    {
                        room.RoomUrl,
                        room.NickName,
                        roomStatus.Platform,
                        roomStatus.RecordStatus,
                        hasRecordUrl = !string.IsNullOrWhiteSpace(roomStatus.RecordUrl),
                        hasFlvUrl = !string.IsNullOrWhiteSpace(roomStatus.FlvUrl),
                        hasHlsUrl = !string.IsNullOrWhiteSpace(roomStatus.HlsUrl),
                    });

                    _ = roomStatus.Recorder.Start(new RecorderStartInfo()
                    {
                        NickName = room.NickName,
                        FlvUrl = roomStatus.FlvUrl,
                        HlsUrl = roomStatus.HlsUrl,
                        RecordUrl = roomStatus.RecordUrl,
                        RoomUrl = room.RoomUrl,
                        Platform = roomStatus.Platform,
                        Resolution = roomStatus.Resolution,
                        Headers = roomStatus.Headers,
                        Title = roomStatus.Title,
                        Bitrate = roomStatus.Bitrate,
                        CoverPath = roomStatus.AvatarLocalPath,
                    });
                }
            }
            else if (roomStatus.RecordStatus != RecordStatus.Recording)
            {
                roomStatus.RecordStatus = RecordStatus.NotRecording;
            }
        }
        else
        {
            if (roomStatus.RecordStatus == RecordStatus.Recording)
            {
                AppSessionLogger.Event("info", "business", "record_stop_requested", "record stop requested because recording is disabled", new
                {
                    room.RoomUrl,
                    room.NickName,
                    roomStatus.Platform,
                    roomStatus.RecordStatus,
                });
                roomStatus.Recorder.Stop();
            }

            roomStatus.RecordStatus = RecordStatus.Disabled;
        }

        if (shouldNotify && prevStreamStatus != StreamStatus.Streaming && isLiveStreaming)
        {
            await Notify(room, token);
        }
    }

    private static bool IsLiveStreaming(ISpiderResult spiderResult)
    {
        return spiderResult.IsLiveStreaming == true || HasRecordableStream(spiderResult);
    }

    private static bool HasRecordableStream(ISpiderResult spiderResult)
    {
        return !string.IsNullOrWhiteSpace(spiderResult.RecordUrl) ||
               !string.IsNullOrWhiteSpace(spiderResult.HlsUrl) ||
               !string.IsNullOrWhiteSpace(spiderResult.FlvUrl);
    }

    private static bool HasRecordableStream(RoomStatus roomStatus)
    {
        return !string.IsNullOrWhiteSpace(roomStatus.RecordUrl) ||
               !string.IsNullOrWhiteSpace(roomStatus.HlsUrl) ||
               !string.IsNullOrWhiteSpace(roomStatus.FlvUrl);
    }

    private static bool IsUsableResolution(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(new[] { 'x', 'X', '*' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               int.TryParse(parts[0], out int width) &&
               int.TryParse(parts[1], out int height) &&
               width >= 320 &&
               height >= 180;
    }

    private static IEnumerable<Room> DistinctRoomsByUrl(IEnumerable<Room> rooms)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (Room room in rooms)
        {
            if (string.IsNullOrWhiteSpace(room.RoomUrl))
            {
                continue;
            }

            if (seen.Add(room.RoomUrl))
            {
                yield return room;
            }
        }
    }

    /// <summary>
    /// Get Room Status
    /// </summary>
    private static RoomStatus? TryGetRoomStatus(Room room)
    {
        // First insert
        if (!RoomStatus.ContainsKey(room.RoomUrl))
        {
            RoomStatus.TryAdd(room.RoomUrl, new RoomStatus()
            {
                NickName = room.NickName,
                RoomUrl = room.RoomUrl,
                AvatarLocalPath = AvatarCache.GetCachedAvatarSource(room.RoomUrl),
                FlvUrl = null!,
                HlsUrl = null!,
                RecordUrl = null!,
                Platform = string.IsNullOrWhiteSpace(room.Platform) ? PlatformDetector.DetectFromUrl(room.RoomUrl) : room.Platform,
                Title = null!,
                Uid = null!,
                Quality = null!,
                Resolution = null!,
                Bitrate = null!,
                Headers = null!,
                StreamStatus = StreamStatus.Initialized,
            });
        }

        if (RoomStatus.TryGetValue(room.RoomUrl, out RoomStatus? roomStatus))
        {
            ///
        }

        return roomStatus;
    }

    /// <summary>
    /// Notification Runnable
    /// </summary>
    private static async Task Notify(Room room, CancellationToken token = default)
    {
        if (Configurations.IsToNotifyWithSystem.Get())
        {
            Notifier.AddNoticeWithButton("LiveNotification".Tr(), room.NickName, [
                new ToastContentButtonOption()
                {
                    Content = "GotoLiveRoom".Tr(),
                    Arguments = [("RoomUrl", room.RoomUrl)],
                    ActivationType = ToastActivationType.Background,
                },
                new ToastContentButtonOption()
                {
                    Content = "ButtonOfClose".Tr(),
                    ActivationType = ToastActivationType.Foreground,
                },
            ]);
        }

        if (Configurations.IsToNotifyWithMusic.Get())
        {
            _ = Task.Run(async () =>
            {
                const string musicPack = "pack://application:,,,/Assets/b_101.f1304dc4.mp3";
                string? musicPath = Configurations.ToNotifyWithMusicPath.Get();

                if (File.Exists(musicPath))
                {
                    using MediaInfo lib = new();
                    lib.Open(musicPath);
                    string audioTrackCount = lib.Get(StreamKind.Audio, 0, "StreamCount");

                    if (int.TryParse(audioTrackCount, out int count) && count > 0)
                    {
                        using FileStream stream = File.OpenRead(musicPath);
                        await Notifier.PlayMusicAsync(stream);
                    }
                    else
                    {
                        using Stream stream = ResourcesProvider.GetStream(musicPack);
                        await Notifier.PlayMusicAsync(stream);
                    }
                }
                else
                {
                    using Stream stream = ResourcesProvider.GetStream(musicPack);
                    await Notifier.PlayMusicAsync(stream);
                }
            }, token);
        }

        if (Configurations.IsToNotifyWithEmail.Get())
        {
            string smtpServer = Configurations.ToNotifyWithEmailSmtp.Get();
            string userName = Configurations.ToNotifyWithEmailUserName.Get();
            string password = Configurations.ToNotifyWithEmailPassword.Get();

            _ = Task.Run(() =>
            {
                _ = Notifier.SendEmail(smtpServer, userName, password, room.NickName, room.RoomUrl);
            }, token);
        }

        if (Configurations.IsToNotifyGotoRoomUrl.Get())
        {
            // TODO: Implement for other platforms
            _ = await Launcher.LaunchUriAsync(new Uri(room.RoomUrl));

            if (Configurations.IsToNotifyGotoRoomUrlAndMute.Get())
            {
                SystemVolume.SetMasterVolumeMute(true);
            }
        }
    }
}

