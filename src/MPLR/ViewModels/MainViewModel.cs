using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ComputedConverters;
using Fischless.Configuration;
using Flucli;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MPLR.Core;
using MPLR.Extensions;
using MPLR.Models;
using MPLR.Threading;
using MPLR.Views;
using Vanara.PInvoke;
using Windows.Storage;
using Windows.System;
using Wpf.Ui.Violeta.Controls;
using Wpf.Ui.Violeta.Threading;
using CheckBox = System.Windows.Controls.CheckBox;

namespace MPLR.ViewModels;

[ObservableObject]
public partial class MainViewModel : ReactiveObject
{
    protected internal ForeverDispatcherTimer DispatcherTimer { get; }

    [ObservableProperty]
    private ReactiveCollection<RoomStatusReactive> roomStatuses = [];

    [ObservableProperty]
    private RoomStatusReactive selectedItem = new();

    [ObservableProperty]
    private bool isRecording = false;

    [ObservableProperty]
    private bool isCardEditMode = false;

    [ObservableProperty]
    private bool isRefreshingSelectedRoomInfo = false;

    partial void OnIsRecordingChanged(bool value)
    {
        TrayIconManager.GetInstance().UpdateTrayIcon();
    }

    [ObservableProperty]
    private bool statusOfIsMonitorRunning = Configurations.IsMonitorRunning.Get();

    [ObservableProperty]
    private bool statusOfIsToNotify = Configurations.IsToNotify.Get();

    [ObservableProperty]
    private bool statusOfIsToRecord = Configurations.IsToRecord.Get();

    [ObservableProperty]
    private bool statusOfIsUseProxy = Configurations.IsUseProxy.Get();

    [ObservableProperty]
    private bool statusOfIsUseKeepAwake = Configurations.IsUseKeepAwake.Get();

    [ObservableProperty]
    private bool statusOfIsUseAutoShutdown = Configurations.IsUseAutoShutdown.Get();

    [ObservableProperty]
    private string statusOfAutoShutdownTime = Configurations.AutoShutdownTime.Get();

    [ObservableProperty]
    private string statusOfRecordFormat = Configurations.RecordFormat.Get();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusOfRoutineIntervalWithUnit))]
    private int statusOfRoutineInterval = Configurations.RoutineInterval.Get();

    public string StatusOfRoutineIntervalWithUnit
        => RoutineIntervalUnitHelper.FormatDisplayValue(StatusOfRoutineInterval);

    [ObservableProperty]
    private bool isReadyToShutdown = false;

    public CancellationTokenSource? ShutdownCancellationTokenSource { get; private set; } = null;

    public MainViewModel()
    {
        DispatcherTimer = new(TimeSpan.FromSeconds(3), ReloadRoomStatus);

        Room[] rooms = EnsureRoomAddedAt(Configurations.Rooms.Get());

        RoomStatuses.Reset(rooms.Select(room =>
        {
            RoomStatusReactive reactive = new()
            {
                NickName = room.NickName,
                RoomUrl = room.RoomUrl,
                IsToNotify = room.IsToNotify,
                IsToRecord = room.IsToRecord,
                IsToMonitor = room.IsToMonitor,
                IsFollowGlobalSettings = room.IsFollowGlobalSettings,
                AddedAt = room.AddedAt,
            };
            RoomInfoCache.Apply(room, reactive);
            return reactive;
        }));

        if (RoomStatuses.Count > 0)
        {
            SelectedItem = RoomStatuses[0];
        }

        Locale.CultureChanged += (_, _) =>
        {
            foreach (RoomStatusReactive roomStatusReactive in RoomStatuses)
            {
                roomStatusReactive.RefreshStatus();
            }
        };

        WeakReferenceMessenger.Default.Register<ToastNotificationActivatedMessage>(this, (_, msg) =>
        {
            string arguments = msg.EventArgs.Argument;

            if (!string.IsNullOrEmpty(arguments))
            {
                NameValueCollection parsedArgs = HttpUtility.ParseQueryString(arguments);

                if (parsedArgs["AutoShutdownCancel"] != null)
                {
                    ShutdownCancellationTokenSource?.Cancel();
                }
            }
        });

        if (Configurations.IsMonitorRunning.Get())
        {
            GlobalMonitor.Start();
        }
        ChildProcessTracerPeriodicTimer.Default.WhiteList = ["ffmpeg", "ffprobe", "ffplay", "python", "python3"];
        ChildProcessTracerPeriodicTimer.Default.Start();
        DispatcherTimer.Start();
        _ = RefreshRoomCardsAsync(showToast: false);
    }

    private void ReloadRoomStatus()
    {
        foreach (RoomStatus roomStatus in GlobalMonitor.RoomStatus.Values.ToArray())
        {
            RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(room => room.RoomUrl == roomStatus.RoomUrl).FirstOrDefault();

            if (roomStatusReactive != null)
            {
                roomStatusReactive.AvatarThumbUrl = roomStatus.AvatarThumbUrl;
                roomStatusReactive.AvatarLocalPath = roomStatus.AvatarLocalPath;
                roomStatusReactive.StreamStatus = roomStatus.StreamStatus;
                roomStatusReactive.RecordStatus = roomStatus.RecordStatus;
                roomStatusReactive.FlvUrl = roomStatus.FlvUrl;
                roomStatusReactive.HlsUrl = roomStatus.HlsUrl;
                roomStatusReactive.RecordUrl = roomStatus.RecordUrl;
                roomStatusReactive.Platform = roomStatus.Platform;
                roomStatusReactive.Title = roomStatus.Title;
                roomStatusReactive.Uid = roomStatus.Uid;
                roomStatusReactive.Quality = roomStatus.Quality;
                roomStatusReactive.Resolution = roomStatus.Resolution;
                roomStatusReactive.Bitrate = roomStatus.Bitrate;
                roomStatusReactive.Headers = roomStatus.Headers;
                roomStatusReactive.StartTime = roomStatus.Recorder.StartTime;
                roomStatusReactive.EndTime = roomStatus.Recorder.EndTime;
                roomStatusReactive.RefreshDuration();
            }
        }

        IsRecording = RoomStatuses.Any(roomStatusReactive => roomStatusReactive.RecordStatus == RecordStatus.Recording);

        StatusOfIsMonitorRunning = Configurations.IsMonitorRunning.Get();
        StatusOfIsToNotify = Configurations.IsToNotify.Get();
        StatusOfIsToRecord = Configurations.IsToRecord.Get();
        StatusOfIsUseProxy = Configurations.IsUseProxy.Get();
        StatusOfIsUseKeepAwake = Configurations.IsUseKeepAwake.Get();
        StatusOfIsUseAutoShutdown = Configurations.IsUseAutoShutdown.Get();
        StatusOfAutoShutdownTime = Configurations.AutoShutdownTime.Get();
        StatusOfRecordFormat = Configurations.RecordFormat.Get();
        StatusOfRoutineInterval = Configurations.RoutineInterval.Get();

        foreach (RoomStatusReactive roomStatusReactive in RoomStatuses)
        {
            roomStatusReactive.RefreshStatus();
        }

        if (StatusOfIsUseAutoShutdown && TimeSpan.TryParse(StatusOfAutoShutdownTime, out TimeSpan targetTime))
        {
            int timeOffset = (int)(DateTime.Now.TimeOfDay - targetTime).TotalSeconds;

            if (timeOffset >= 0 && timeOffset <= 60)
            {
                IsReadyToShutdown = true;
            }

            if (IsReadyToShutdown && !IsRecording)
            {
                if (ShutdownCancellationTokenSource == null)
                {
                    ShutdownCancellationTokenSource = new();

                    Notifier.AddNoticeWithButton("Title".Tr(), "AutoShutdownInTime".Tr(), [
                        new ToastContentButtonOption()
                            {
                                Content = "ButtonOfCancel".Tr(),
                                Arguments = [("AutoShutdownCancel", string.Empty)],
                                ActivationType = ToastActivationType.Foreground,
                            }
                    ]);

                    ApplicationDispatcher.BeginInvoke(async () =>
                    {
                        await Task.Delay(60000);

                        if (!ShutdownCancellationTokenSource.IsCancellationRequested && !IsRecording)
                        {
                            if (Debugger.IsAttached)
                            {
                                _ = MessageBox.Information("AutoShutdown".Tr());
                            }
                            else
                            {
                                _ = Interop.ExitWindowsEx(User32.ExitWindowsFlags.EWX_SHUTDOWN | User32.ExitWindowsFlags.EWX_FORCE);
                            }
                        }

                        ShutdownCancellationTokenSource = null;
                        IsReadyToShutdown = false;
                    });
                }
            }
        }
    }

    [RelayCommand]
    private async Task AddRoomAsync()
    {
        AddRoomContentDialog dialog = new();
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (!string.IsNullOrWhiteSpace(dialog.NickName))
            {
                await AddRoomToListAsync(dialog.Url, dialog.RoomUrl!, dialog.NickName, dialog.SpiderResult, dialog.IsToNotify, dialog.IsFollowGlobalSettings);
            }
        }
    }

    public async Task<bool> TryAddRoomFromFlyoutAsync(string? url, bool isForcedAdd, bool isToNotify, bool isFollowGlobalSettings)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Toast.Warning("EnterRoomUrl".Tr());
            return false;
        }

        if (isForcedAdd)
        {
            string? roomUrl = Spider.ParseUrl(url);

            if (roomUrl == null)
            {
                Toast.Error("ErrorRoomUrl".Tr());
                return false;
            }

            if (Configurations.Rooms.Get().Any(room => room.RoomUrl == roomUrl))
            {
                Toast.Warning("AddRoomErrorDuplicated".Tr(roomUrl));
                return false;
            }

            await AddRoomToListAsync(url, roomUrl, roomUrl, null, isToNotify, isFollowGlobalSettings);
            Toast.Success("AddRoomSucc".Tr(roomUrl));
            return true;
        }

        using (LoadingWindow.ShowAsync())
        {
            try
            {
                ISpiderResult? spider = await Task.Run(() => Spider.GetResult(url));

                if (string.IsNullOrWhiteSpace(spider?.Nickname) || string.IsNullOrWhiteSpace(spider.RoomUrl))
                {
                    Toast.Error(GetRoomInfoErrorMessage());
                    return false;
                }

                if (Configurations.Rooms.Get().Any(room => room.RoomUrl == spider.RoomUrl))
                {
                    Toast.Warning("AddRoomErrorDuplicated".Tr(spider.Nickname));
                    return false;
                }

                await AddRoomToListAsync(url, spider.RoomUrl, spider.Nickname, spider, isToNotify, isFollowGlobalSettings);
                Toast.Success("AddRoomSucc".Tr(spider.Nickname));
                return true;
            }
            catch (Exception exception)
            {
                Toast.Error(GetRoomInfoErrorMessage(exception.Message));
                return false;
            }
        }
    }

    private async Task AddRoomToListAsync(string? originalUrl, string roomUrl, string nickName, ISpiderResult? spiderResult, bool isToNotify, bool isFollowGlobalSettings)
    {
        List<Room> rooms = [.. Configurations.Rooms.Get()];

        Room newRoom = new()
        {
            NickName = nickName,
            RoomUrl = roomUrl,
            AddedAt = DateTime.Now,
            IsToNotify = isToNotify,
            IsFollowGlobalSettings = isFollowGlobalSettings,
        };
        rooms.RemoveAll(room => room.RoomUrl == originalUrl || room.RoomUrl == roomUrl);
        rooms.Add(newRoom);
        Configurations.Rooms.Set([.. rooms]);
        ConfigurationManager.Save();

        RoomStatusReactive roomStatusReactive = new()
        {
            NickName = nickName,
            RoomUrl = roomUrl,
            AddedAt = DateTime.Now,
            AvatarLocalPath = AvatarCache.GetCachedAvatarSource(roomUrl),
            IsToNotify = isToNotify,
            IsFollowGlobalSettings = isFollowGlobalSettings,
        };

        if (spiderResult != null)
        {
            string avatarLocalPath = string.IsNullOrWhiteSpace(spiderResult.AvatarThumbUrl)
                ? AvatarCache.GetCachedAvatarSource(roomUrl)
                : await AvatarCache.UpdateAsync(roomUrl, spiderResult.AvatarThumbUrl);
            ApplyRoomCardRefresh(roomStatusReactive, spiderResult, avatarLocalPath, MediaProbeResult.Empty);
            RoomInfoCache.Apply(roomStatusReactive, newRoom);
            Configurations.Rooms.Set([.. rooms]);
            ConfigurationManager.Save();
        }

        RoomStatuses.Add(roomStatusReactive);
        SelectedItem = roomStatusReactive;
    }

    private static string GetRoomInfoErrorMessage(string? fallback = null)
    {
        string error = ExternalStreamResolver.LastError;
        string message = string.IsNullOrWhiteSpace(error) ? fallback ?? string.Empty : error;

        if (IsCookieOrRiskError(message))
        {
            return "GetRoomInfoCookieError".Tr();
        }

        string genericMessage = "GetRoomInfoError".Tr();
        return string.IsNullOrWhiteSpace(message) ? genericMessage : $"{genericMessage}: {message}";
    }

    private static bool IsCookieOrRiskError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string value = message.ToLowerInvariant();
        string[] keywords =
        [
            "cookie",
            "login",
            "captcha",
            "forbidden",
            "blocked",
            "ip banned",
            "403",
            "401",
            "登录",
            "登陆",
            "验证码",
            "风控",
            "请求过快",
            "操作太快",
            "频繁",
        ];

        return keywords.Any(value.Contains);
    }

    [RelayCommand]
    private void OpenSettingsDialog()
    {
        foreach (Window win in Application.Current.Windows.OfType<SettingsWindow>())
        {
            win.Close();
        }

        _ = new SettingsWindow()
        {
            Owner = Application.Current.MainWindow,
        }.ShowDialog();
    }

    [RelayCommand]
    private async Task OpenSaveFolderAsync()
    {
        // TODO: Implement for other platforms
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(
                SaveFolderHelper.GetSaveFolder(Configurations.SaveFolder.Get())
            )
        );
    }

    [RelayCommand]
    private async Task OpenSettingsFileFolderAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await "explorer"
                .WithArguments($"/select,\"{ConfigurationManager.FilePath}\"")
                .ExecuteAsync();
        }
        else
        {
            // TODO: Implement for other platforms
            await Launcher.LaunchUriAsync(new Uri(ConfigurationManager.FilePath));
        }
    }

    [RelayCommand]
    private async Task OpenAboutAsync()
    {
        AboutContentDialog dialog = new();
        _ = await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task OpenHyperlink(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            _ = await Launcher.LaunchUriAsync(uri);
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (GlobalMonitor.RoomStatus.TryGetValue(SelectedItem.RoomUrl, out RoomStatus? roomStatus))
        {
            await Player.PreviewAsync(
                roomStatus.RoomUrl,
                roomStatus.NickName,
                roomStatus.RecordUrl,
                roomStatus.HlsUrl,
                roomStatus.FlvUrl,
                roomStatus.Headers,
                roomStatus.Title);
        }
        else
        {
            await Player.PreviewAsync(
                SelectedItem.RoomUrl,
                SelectedItem.NickName,
                SelectedItem.RecordUrl,
                SelectedItem.HlsUrl,
                SelectedItem.FlvUrl,
                SelectedItem.Headers,
                SelectedItem.Title);
        }
    }

    [RelayCommand]
    private async Task ToggleMonitorAsync()
    {
        bool isMonitorRunning = !Configurations.IsMonitorRunning.Get();
        Configurations.IsMonitorRunning.Set(isMonitorRunning);
        ConfigurationManager.Save();
        StatusOfIsMonitorRunning = isMonitorRunning;

        if (isMonitorRunning)
        {
            GlobalMonitor.Start();
            await GlobalMonitor.RunOnceAsync();
            Toast.Success("MonitorStarted".Tr());
        }
        else
        {
            GlobalMonitor.Stop();
            Toast.Error("MonitorStopped".Tr());
        }
    }

    [RelayCommand]
    private async Task ToggleRecordAsync()
    {
        bool isToRecord = !Configurations.IsToRecord.Get();
        Configurations.IsToRecord.Set(isToRecord);
        ConfigurationManager.Save();
        StatusOfIsToRecord = isToRecord;

        if (isToRecord && Configurations.IsMonitorRunning.Get())
        {
            GlobalMonitor.Start();
            await GlobalMonitor.RunOnceAsync();
        }
        else if (!isToRecord)
        {
            foreach (RoomStatus roomStatus in GlobalMonitor.RoomStatus.Values)
            {
                if (roomStatus.RecordStatus == RecordStatus.Recording)
                {
                    roomStatus.Recorder.Stop();
                }
            }
        }

        if (isToRecord)
        {
            Toast.Success("RecordStarted".Tr());
        }
        else
        {
            Toast.Error("RecordStopped".Tr());
        }
    }

    [RelayCommand]
    private void ToggleNotify()
    {
        bool value = !Configurations.IsToNotify.Get();
        Configurations.IsToNotify.Set(value);
        ConfigurationManager.Save();
        StatusOfIsToNotify = value;
    }

    [RelayCommand]
    private void ToggleProxy()
    {
        bool value = !Configurations.IsUseProxy.Get();
        Configurations.IsUseProxy.Set(value);
        ConfigurationManager.Save();
        StatusOfIsUseProxy = value;
    }

    [RelayCommand]
    private void ToggleKeepAwake()
    {
        bool value = !Configurations.IsUseKeepAwake.Get();
        Configurations.IsUseKeepAwake.Set(value);
        ConfigurationManager.Save();
        StatusOfIsUseKeepAwake = value;
        _ = value
            ? Kernel32.SetThreadExecutionState(Kernel32.EXECUTION_STATE.ES_CONTINUOUS | Kernel32.EXECUTION_STATE.ES_SYSTEM_REQUIRED | Kernel32.EXECUTION_STATE.ES_AWAYMODE_REQUIRED)
            : Kernel32.SetThreadExecutionState(Kernel32.EXECUTION_STATE.ES_CONTINUOUS);
    }

    [RelayCommand]
    private void ToggleAutoShutdown()
    {
        bool value = !Configurations.IsUseAutoShutdown.Get();
        Configurations.IsUseAutoShutdown.Set(value);
        ConfigurationManager.Save();
        StatusOfIsUseAutoShutdown = value;
    }

    [RelayCommand]
    private void SetRoutineInterval(string? value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int interval))
        {
            return;
        }

        interval = int.Max(interval, 500);
        Configurations.RoutineInterval.Set(interval);
        ConfigurationManager.Save();
        GlobalMonitor.RoutinePeriodicWait.Period = TimeSpan.FromMilliseconds(interval);
        StatusOfRoutineInterval = interval;
    }

    [RelayCommand]
    private void SetRecordFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Configurations.RecordFormat.Set(value);
        ConfigurationManager.Save();
        StatusOfRecordFormat = value;

        foreach (RoomStatusReactive roomStatusReactive in RoomStatuses)
        {
            roomStatusReactive.RefreshStatus();
        }
    }

    [RelayCommand]
    private void OpenTranscodeSettings()
    {
        OpenSettingsDialog();
    }

    [RelayCommand]
    private void SortRoomsByName()
    {
        RoomStatusReactive? selected = SelectedItem;
        RoomStatuses.Reset(RoomStatuses
            .OrderBy(room => room.NickName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(room => room.RoomUrl, StringComparer.OrdinalIgnoreCase)
            .ToArray());
        RestoreSelectedRoom(selected);
        SaveRoomOrder();
    }

    [RelayCommand]
    private void SortRoomsByAddedAt()
    {
        RoomStatusReactive? selected = SelectedItem;
        RoomStatuses.Reset(RoomStatuses
            .OrderBy(room => room.AddedAt == DateTime.MinValue ? DateTime.MaxValue : room.AddedAt)
            .ThenBy(room => room.RoomUrl, StringComparer.OrdinalIgnoreCase)
            .ToArray());
        RestoreSelectedRoom(selected);
        SaveRoomOrder();
    }

    [RelayCommand]
    private async Task RefreshRoomCardsAsync()
    {
        await RefreshRoomCardsAsync(showToast: true);
    }

    private async Task RefreshRoomCardsAsync(bool showToast)
    {
        RoomStatusReactive[] rooms = [.. RoomStatuses];
        WeakReferenceMessenger.Default.Send(new RoomCardsFlashMessage());

        (RoomStatusReactive Room, ISpiderResult? Result, string AvatarLocalPath, MediaProbeResult Probe)[] results = await RefreshRoomsInParallelAsync(rooms, true);

        foreach ((RoomStatusReactive room, ISpiderResult? result, string avatarLocalPath, MediaProbeResult probe) in results)
        {
            ApplyRoomCardRefresh(room, result, avatarLocalPath, probe);
        }

        SaveRoomOrder();

        if (showToast)
        {
            Toast.Success("RoomCardsRefreshed".Tr());
        }
    }

    private static async Task<(RoomStatusReactive Room, ISpiderResult? Result, string AvatarLocalPath, MediaProbeResult Probe)[]> RefreshRoomsInParallelAsync(RoomStatusReactive[] rooms, bool probeMissingOnly)
    {
        using SemaphoreSlim semaphore = new(Math.Clamp(Environment.ProcessorCount, 4, 8));
        Task<(RoomStatusReactive Room, ISpiderResult? Result, string AvatarLocalPath, MediaProbeResult Probe)>[] tasks = rooms.Select(async room =>
        {
            await semaphore.WaitAsync();
            try
            {
                ISpiderResult? result = string.IsNullOrWhiteSpace(room.RoomUrl) ? null : await Task.Run(() => Spider.GetResult(room.RoomUrl));
                string avatarLocalPath = result == null || string.IsNullOrWhiteSpace(result.AvatarThumbUrl)
                    ? AvatarCache.GetCachedAvatarSource(room.RoomUrl)
                    : await AvatarCache.UpdateAsync(room.RoomUrl, result.AvatarThumbUrl);
                string streamUrl = SelectProbeUrl(result, room);
                bool needsResolution = !IsUsableResolutionText(result?.Resolution) && !IsUsableResolutionText(room.Resolution);
                bool needsBitrate = string.IsNullOrWhiteSpace(result?.Bitrate) && string.IsNullOrWhiteSpace(room.Bitrate);
                bool needsProbe = !string.IsNullOrWhiteSpace(streamUrl) && (!probeMissingOnly || needsResolution || needsBitrate);
                MediaProbeResult probe = needsProbe
                    ? await MediaProbe.ProbeAsync(streamUrl, result?.Headers ?? room.Headers)
                    : MediaProbeResult.Empty;

                return (room, result, avatarLocalPath, probe);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        return await Task.WhenAll(tasks);
    }

    [RelayCommand]
    private void ToggleCardEditMode()
    {
        IsCardEditMode = !IsCardEditMode;
        if (IsCardEditMode)
        {
            Toast.Success("CardEditModeOn".Tr());
        }
        else
        {
            Toast.Error("CardEditModeOff".Tr());
        }
    }

    [RelayCommand]
    private void ExitApplication()
    {
        TrayIconManager.GetInstance().RequestShutdown();
    }

    [RelayCommand]
    private void RowUpRoomUrl()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(roomStatus => roomStatus.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();
        if (roomStatusReactive != null)
        {
            RoomStatuses.MoveUp(roomStatusReactive);
            SaveRoomOrder();
        }
    }

    [RelayCommand]
    private void RowDownRoomUrl()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(roomStatus => roomStatus.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();
        if (roomStatusReactive != null)
        {
            RoomStatuses.MoveDown(roomStatusReactive);
            SaveRoomOrder();
        }
    }

    public void MoveRoom(RoomStatusReactive source, RoomStatusReactive target)
    {
        int oldIndex = RoomStatuses.IndexOf(source);
        int newIndex = RoomStatuses.IndexOf(target);

        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        RoomStatuses.Move(oldIndex, newIndex);
        SelectedItem = source;
        SaveRoomOrder();
    }

    public void MoveRoom(RoomStatusReactive source, int newIndex)
    {
        int oldIndex = RoomStatuses.IndexOf(source);

        if (oldIndex < 0)
        {
            return;
        }

        newIndex = Math.Clamp(newIndex, 0, Math.Max(0, RoomStatuses.Count - 1));

        if (oldIndex == newIndex)
        {
            return;
        }

        RoomStatuses.Move(oldIndex, newIndex);
        SelectedItem = source;
        SaveRoomOrder();
    }

    private void SaveRoomOrder()
    {
        Dictionary<string, Room> rooms = Configurations.Rooms.Get()
            .Where(room => !string.IsNullOrWhiteSpace(room.RoomUrl))
            .GroupBy(room => room.RoomUrl)
            .ToDictionary(group => group.Key, group => group.Last());

        List<Room> orderedRooms = [];

        foreach (RoomStatusReactive roomStatusReactive in RoomStatuses)
        {
            if (string.IsNullOrWhiteSpace(roomStatusReactive.RoomUrl))
            {
                continue;
            }

            if (!rooms.TryGetValue(roomStatusReactive.RoomUrl, out Room? room))
            {
                room = new Room();
            }

            room.NickName = roomStatusReactive.NickName;
            room.RoomUrl = roomStatusReactive.RoomUrl;
            room.IsToNotify = roomStatusReactive.IsToNotify;
            room.IsToRecord = roomStatusReactive.IsToRecord;
            room.IsToMonitor = roomStatusReactive.IsToMonitor;
            room.IsFollowGlobalSettings = roomStatusReactive.IsFollowGlobalSettings;
            room.AddedAt = roomStatusReactive.AddedAt == DateTime.MinValue ? DateTime.Now : roomStatusReactive.AddedAt;
            RoomInfoCache.Apply(roomStatusReactive, room);
            orderedRooms.Add(room);
        }

        Configurations.Rooms.Set([.. orderedRooms]);
        ConfigurationManager.Save();
    }

    private static Room[] EnsureRoomAddedAt(Room[] rooms)
    {
        bool changed = false;
        DateTime baseTime = DateTime.Now.AddSeconds(-rooms.Length);

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i].AddedAt == DateTime.MinValue)
            {
                rooms[i].AddedAt = baseTime.AddSeconds(i);
                changed = true;
            }
        }

        if (changed)
        {
            Configurations.Rooms.Set(rooms);
            ConfigurationManager.Save();
        }

        return rooms;
    }

    private void RestoreSelectedRoom(RoomStatusReactive? selected)
    {
        if (selected == null || string.IsNullOrWhiteSpace(selected.RoomUrl))
        {
            SelectedItem = RoomStatuses.FirstOrDefault() ?? new RoomStatusReactive();
            return;
        }

        SelectedItem = RoomStatuses.FirstOrDefault(room => room.RoomUrl == selected.RoomUrl) ?? RoomStatuses.FirstOrDefault() ?? new RoomStatusReactive();
    }

    [RelayCommand]
    private async Task RefreshSelectedRoomInfoAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        IsRefreshingSelectedRoomInfo = true;
        try
        {
            ISpiderResult? result = await Task.Run(() => Spider.GetResult(SelectedItem.RoomUrl));
            string avatarLocalPath = result == null || string.IsNullOrWhiteSpace(result.AvatarThumbUrl)
                ? AvatarCache.GetCachedAvatarSource(SelectedItem.RoomUrl)
                : await AvatarCache.UpdateAsync(SelectedItem.RoomUrl, result.AvatarThumbUrl);
            string streamUrl = SelectProbeUrl(result, SelectedItem);
            MediaProbeResult probe = string.IsNullOrWhiteSpace(streamUrl)
                ? MediaProbeResult.Empty
                : await MediaProbe.ProbeAsync(streamUrl, result?.Headers ?? SelectedItem.Headers);

            ApplyRoomCardRefresh(SelectedItem, result, avatarLocalPath, probe);
            SaveRoomOrder();
            Toast.Success("RoomInfoRefreshed".Tr());
        }
        finally
        {
            IsRefreshingSelectedRoomInfo = false;
        }
    }

    private static void ApplyRoomCardRefresh(RoomStatusReactive room, ISpiderResult? spiderResult, string avatarLocalPath, MediaProbeResult probe)
    {
        if (!string.IsNullOrWhiteSpace(avatarLocalPath))
        {
            room.AvatarLocalPath = avatarLocalPath;
        }

        if (spiderResult == null)
        {
            if (!string.IsNullOrWhiteSpace(probe.Resolution))
            {
                room.Resolution = probe.Resolution;
            }

            if (!string.IsNullOrWhiteSpace(probe.Bitrate))
            {
                room.Bitrate = probe.Bitrate;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Nickname))
        {
            room.NickName = spiderResult.Nickname;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.AvatarThumbUrl))
        {
            room.AvatarThumbUrl = spiderResult.AvatarThumbUrl;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.FlvUrl))
        {
            room.FlvUrl = spiderResult.FlvUrl;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.HlsUrl))
        {
            room.HlsUrl = spiderResult.HlsUrl;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.RecordUrl))
        {
            room.RecordUrl = spiderResult.RecordUrl;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Platform))
        {
            room.Platform = spiderResult.Platform;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Title))
        {
            room.Title = spiderResult.Title;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Uid))
        {
            room.Uid = spiderResult.Uid;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Quality))
        {
            room.Quality = spiderResult.Quality;
        }

        if (IsUsableResolutionText(spiderResult.Resolution))
        {
            room.Resolution = spiderResult.Resolution!;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Bitrate))
        {
            room.Bitrate = spiderResult.Bitrate;
        }

        if (IsUsableResolutionText(probe.Resolution))
        {
            room.Resolution = probe.Resolution;
        }

        if (!string.IsNullOrWhiteSpace(probe.Bitrate))
        {
            room.Bitrate = probe.Bitrate;
        }

        if (!string.IsNullOrWhiteSpace(spiderResult.Headers))
        {
            room.Headers = spiderResult.Headers;
        }

        room.StreamStatus = spiderResult.IsLiveStreaming switch
        {
            true => StreamStatus.Streaming,
            false => StreamStatus.NotStreaming,
            _ => HasRecordableStream(spiderResult) ? StreamStatus.Streaming : room.StreamStatus,
        };

        if (GlobalMonitor.RoomStatus.TryGetValue(room.RoomUrl, out RoomStatus? roomStatus))
        {
            roomStatus.NickName = room.NickName;
            roomStatus.AvatarThumbUrl = room.AvatarThumbUrl;
            roomStatus.AvatarLocalPath = room.AvatarLocalPath;
            roomStatus.FlvUrl = room.FlvUrl;
            roomStatus.HlsUrl = room.HlsUrl;
            roomStatus.RecordUrl = room.RecordUrl;
            roomStatus.Platform = room.Platform;
            roomStatus.Title = room.Title;
            roomStatus.Uid = room.Uid;
            roomStatus.Quality = room.Quality;
            roomStatus.Resolution = room.Resolution;
            roomStatus.Bitrate = room.Bitrate;
            roomStatus.Headers = room.Headers;
            roomStatus.StreamStatus = room.StreamStatus;
        }

        RoomInfoCache.Save(room);
    }

    private static string SelectProbeUrl(ISpiderResult? result, RoomStatusReactive room)
    {
        return SelectFirstNonWhite(result?.RecordUrl, result?.HlsUrl, result?.FlvUrl, room.RecordUrl, room.HlsUrl, room.FlvUrl);
    }

    private static string SelectFirstNonWhite(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static bool HasRecordableStream(ISpiderResult spiderResult)
    {
        return !string.IsNullOrWhiteSpace(spiderResult.RecordUrl) ||
               !string.IsNullOrWhiteSpace(spiderResult.HlsUrl) ||
               !string.IsNullOrWhiteSpace(spiderResult.FlvUrl);
    }

    private static bool IsUsableResolutionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(['x', 'X', '*'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               int.TryParse(parts[0], out int width) &&
               int.TryParse(parts[1], out int height) &&
               width >= 320 &&
               height >= 180;
    }

    [RelayCommand]
    private async Task RemoveRoomUrlAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        string selectedRoomUrl = SelectedItem.RoomUrl;
        MessageBoxResult result = await MessageBox.QuestionAsync("SureRemoveRoom".Tr(SelectedItem.NickName));

        if (result == MessageBoxResult.Yes)
        {
            if (GlobalMonitor.RoomStatus.TryGetValue(selectedRoomUrl, out RoomStatus? roomStatus))
            {
                roomStatus.Recorder.Stop();
                _ = GlobalMonitor.RoomStatus.TryRemove(selectedRoomUrl, out _);
            }

            GlobalMonitor.ClearTemporaryRoomOverrides(selectedRoomUrl);

            RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(room => room.RoomUrl == selectedRoomUrl).FirstOrDefault();
            if (roomStatusReactive != null)
            {
                RoomStatuses.Remove(roomStatusReactive);
            }

            List<Room> rooms = [.. Configurations.Rooms.Get()];

            rooms.RemoveAll(room => room.RoomUrl == selectedRoomUrl);
            Configurations.Rooms.Set([.. rooms]);
            ConfigurationManager.Save();
            SelectedItem = RoomStatuses.FirstOrDefault() ?? new RoomStatusReactive();

            Toast.Success("SuccOp".Tr());
        }
    }

    [RelayCommand]
    private async Task GotoRoomUrlAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        // TODO: Implement for other platforms
        await Launcher.LaunchUriAsync(new Uri(SelectedItem.RoomUrl));
    }

    [RelayCommand]
    private async Task StopRecordAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (GlobalMonitor.RoomStatus.TryGetValue(SelectedItem.RoomUrl, out RoomStatus? roomStatus))
        {
            if (roomStatus.RecordStatus == RecordStatus.Recording)
            {
                StackPanel content = new();
                CheckBox checkBox = new()
                {
                    Content = "EnableRecord".Tr(),
                    DataContext = SelectedItem,
                };

                checkBox.Click += (_, _) =>
                {
                    IsToRecord();
                    Toast.Success("SuccOp".Tr());
                };

                checkBox.SetBinding(CheckBox.IsCheckedProperty, nameof(RoomStatusReactive.IsToRecord));

                content.Children.Add(new TextBlock()
                {
                    Text = "SureStopRecord".Tr(roomStatus.NickName)
                });
                content.Children.Add(checkBox);

                ContentDialog dialog = new()
                {
                    Title = "StopRecord".Tr(),
                    CornerRadius = new CornerRadius(8),
                    Content = content,
                    CloseButtonText = "ButtonOfCancel".Tr(),
                    PrimaryButtonText = "StopRecord".Tr(),
                    DefaultButton = ContentDialogButton.Primary,
                };

                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    roomStatus.Recorder.Stop();
                    Toast.Success("SuccOp".Tr());
                }
            }
            else
            {
                Toast.Warning("NoRecordTask".Tr());
            }
        }
        else
        {
            Toast.Warning("NoRecordTask".Tr());
        }
    }

    [RelayCommand]
    private void ShowRecordLog()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        // TODO
        Toast.Warning("ComingSoon".Tr() + " ...");
    }

    [RelayCommand]
    private void IsToNotify()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (roomStatusReactive != null)
        {
            roomStatusReactive.IsToNotify = SelectedItem.IsToNotify;
        }

        Room[] rooms = Configurations.Rooms.Get();
        Room? room = rooms.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (room != null)
        {
            room.IsToNotify = SelectedItem.IsToNotify;
        }
        Configurations.Rooms.Set(rooms);
        ConfigurationManager.Save();
    }

    [RelayCommand]
    private void IsToRecord()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (SelectedItem.IsFollowGlobalSettings)
        {
            SelectedItem.RefreshStatus();
            return;
        }

        RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (roomStatusReactive != null)
        {
            roomStatusReactive.IsToRecord = SelectedItem.IsToRecord;
        }

        Room[] rooms = Configurations.Rooms.Get();
        Room? room = rooms.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (room != null)
        {
            room.IsToRecord = SelectedItem.IsToRecord;
        }
        Configurations.Rooms.Set(rooms);
        ConfigurationManager.Save();
    }

    [RelayCommand]
    private async Task ToggleSelectedRoomRecordAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (SelectedItem.IsFollowGlobalSettings)
        {
            bool enabled = !GlobalMonitor.GetEffectiveRoomRecord(SelectedItem.RoomUrl, SelectedItem.IsToRecord, true);
            GlobalMonitor.SetTemporaryRoomRecord(SelectedItem.RoomUrl, enabled);
            SelectedItem.RefreshStatus();

            if (enabled && SelectedItem.EffectiveIsToMonitor)
            {
                GlobalMonitor.Start();
                await GlobalMonitor.RunRoomAsync(SelectedItem.RoomUrl);
            }

            if (enabled)
            {
                Toast.Success("RecordStarted".Tr());
            }
            else
            {
                StopSelectedRoomRecording();
                Toast.Error("RecordStopped".Tr());
            }
            return;
        }

        SelectedItem.IsToRecord = !SelectedItem.IsToRecord;
        IsToRecord();
        SelectedItem.RefreshStatus();

        if (SelectedItem.IsToRecord && SelectedItem.EffectiveIsToMonitor)
        {
            if (!Configurations.IsMonitorRunning.Get())
            {
                Configurations.IsMonitorRunning.Set(true);
                ConfigurationManager.Save();
                StatusOfIsMonitorRunning = true;
            }

            GlobalMonitor.Start();
            await GlobalMonitor.RunRoomAsync(SelectedItem.RoomUrl);
        }

        if (SelectedItem.IsToRecord)
        {
            Toast.Success("RecordStarted".Tr());
        }
        else
        {
            StopSelectedRoomRecording();
            Toast.Error("RecordStopped".Tr());
        }
    }

    private void StopSelectedRoomRecording()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (GlobalMonitor.RoomStatus.TryGetValue(SelectedItem.RoomUrl, out RoomStatus? roomStatus) &&
            roomStatus.RecordStatus == RecordStatus.Recording)
        {
            roomStatus.Recorder.Stop();
        }
    }

    [RelayCommand]
    private void IsToMonitor()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (SelectedItem.IsFollowGlobalSettings)
        {
            SelectedItem.RefreshStatus();
            return;
        }

        RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (roomStatusReactive != null)
        {
            roomStatusReactive.IsToMonitor = SelectedItem.IsToMonitor;
        }

        Room[] rooms = Configurations.Rooms.Get();
        Room? room = rooms.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (room != null)
        {
            room.IsToMonitor = SelectedItem.IsToMonitor;
        }

        Configurations.Rooms.Set(rooms);
        ConfigurationManager.Save();
    }

    [RelayCommand]
    private async Task ToggleSelectedRoomMonitorAsync()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        if (SelectedItem.IsFollowGlobalSettings)
        {
            bool enabled = !GlobalMonitor.GetEffectiveRoomMonitor(SelectedItem.RoomUrl, SelectedItem.IsToMonitor, true);
            GlobalMonitor.SetTemporaryRoomMonitor(SelectedItem.RoomUrl, enabled);
            SelectedItem.RefreshStatus();

            if (enabled)
            {
                GlobalMonitor.Start();
                await GlobalMonitor.RunRoomAsync(SelectedItem.RoomUrl);
                Toast.Success("MonitorStarted".Tr());
            }
            else
            {
                Toast.Error("MonitorStopped".Tr());
            }
            return;
        }

        SelectedItem.IsToMonitor = !SelectedItem.IsToMonitor;
        IsToMonitor();
        SelectedItem.RefreshStatus();

        if (SelectedItem.IsToMonitor)
        {
            if (!Configurations.IsMonitorRunning.Get())
            {
                Configurations.IsMonitorRunning.Set(true);
                ConfigurationManager.Save();
                StatusOfIsMonitorRunning = true;
            }

            GlobalMonitor.Start();
            await GlobalMonitor.RunRoomAsync(SelectedItem.RoomUrl);
            Toast.Success("MonitorStarted".Tr());
        }
        else
        {
            Toast.Error("MonitorStopped".Tr());
        }
    }

    [RelayCommand]
    private void IsFollowGlobalSettings()
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.RoomUrl))
        {
            return;
        }

        RoomStatusReactive? roomStatusReactive = RoomStatuses.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (roomStatusReactive != null)
        {
            roomStatusReactive.IsFollowGlobalSettings = SelectedItem.IsFollowGlobalSettings;
        }

        Room[] rooms = Configurations.Rooms.Get();
        Room? room = rooms.Where(room => room.RoomUrl == SelectedItem.RoomUrl).FirstOrDefault();

        if (room != null)
        {
            room.IsFollowGlobalSettings = SelectedItem.IsFollowGlobalSettings;
        }

        Configurations.Rooms.Set(rooms);
        ConfigurationManager.Save();
        GlobalMonitor.ClearTemporaryRoomOverrides(SelectedItem.RoomUrl);
        SelectedItem.RefreshStatus();
    }

    [RelayCommand]
    private void OnContextMenuLoaded(RelayEventParameter param)
    {
        ContextMenu sender = (ContextMenu)param.Deconstruct().Sender;

        sender.Opened -= ContextMenuOpened;
        sender.Opened += ContextMenuOpened;

        // Closure method
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ContextMenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu { } contextMenu
             && contextMenu.Parent is Popup { } popup
             && popup.PlacementTarget is DataGrid { } dataGrid)
            {
                if (dataGrid.InputHitTest(Mouse.GetPosition(dataGrid)) is FrameworkElement { } element)
                {
                    if (GetDataGridRow(element) is DataGridRow { } row)
                    {
                        if (row.DataContext is RoomStatusReactive { } data)
                        {
                            _ = data.MapTo(SelectedItem);

                            foreach (UIElement d in ((ContextMenu)sender).Items.OfType<UIElement>())
                            {
                                d.Visibility = Visibility.Visible;
                            }
                        }
                    }
                    else
                    {
                        ((ContextMenu)sender).IsOpen = false;
                        _ = SelectedItem.MapFrom(new RoomStatusReactive());

                        foreach (UIElement d in ((ContextMenu)sender).Items.OfType<UIElement>())
                        {
                            d.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static DataGridRow? GetDataGridRow(FrameworkElement? element)
            {
                while (element != null && element is not DataGridRow)
                {
                    element = VisualTreeHelper.GetParent(element) as FrameworkElement;
                }
                return element as DataGridRow;
            }
        }
    }
}

internal sealed record RoomCardsFlashMessage;

