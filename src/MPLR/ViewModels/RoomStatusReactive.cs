using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComputedConverters;
using System.Windows;
using MPLR.Core;
using MPLR.Models;
using MPLR.Views;
using Windows.System;
using Wpf.Ui.Violeta.Controls;

namespace MPLR.ViewModels;

[ObservableObject]
public partial class RoomStatusReactive : ReactiveObject
{
    [ObservableProperty]
    private string nickName = string.Empty;

    [ObservableProperty]
    private string avatarThumbUrl = string.Empty;

    [ObservableProperty]
    private string avatarLocalPath = string.Empty;

    public string AvatarDisplaySource => string.IsNullOrWhiteSpace(AvatarLocalPath)
        ? string.IsNullOrWhiteSpace(AvatarThumbUrl)
            ? "pack://application:,,,/Assets/Favicon.png"
            : AvatarThumbUrl
        : AvatarLocalPath;

    partial void OnAvatarLocalPathChanged(string value)
    {
        OnPropertyChanged(nameof(AvatarDisplaySource));
    }

    partial void OnAvatarThumbUrlChanged(string value)
    {
        OnPropertyChanged(nameof(AvatarDisplaySource));
    }

    [ObservableProperty]
    private string roomUrl = string.Empty;

    public string RoomUrlDisplayText => EndEllipsis(RoomUrl, 90);

    partial void OnRoomUrlChanged(string value)
    {
        OnPropertyChanged(nameof(RoomUrlDisplayText));
        OnPropertyChanged(nameof(UidDisplayText));
    }

    [ObservableProperty]
    private DateTime addedAt = DateTime.MinValue;

    [ObservableProperty]
    private string flvUrl = string.Empty;

    [ObservableProperty]
    private string hlsUrl = string.Empty;

    [ObservableProperty]
    private string recordUrl = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlatformDisplayName))]
    [NotifyPropertyChangedFor(nameof(PlatformIconSource))]
    [NotifyPropertyChangedFor(nameof(AvatarDisplaySource))]
    private string platform = string.Empty;

    public string PlatformDisplayName => NormalizePlatformName(Platform);

    public string PlatformIconSource => PlatformKey(Platform) switch
    {
        "douyin" => "pack://application:,,,/Assets/PlatformIcons/douyin.png",
        "tiktok" => "pack://application:,,,/Assets/PlatformIcons/tiktok.png",
        "kuaishou" => "pack://application:,,,/Assets/PlatformIcons/kuaishou.png",
        "huya" => "pack://application:,,,/Assets/PlatformIcons/huya.png",
        "douyu" => "pack://application:,,,/Assets/PlatformIcons/douyu.png",
        "bilibili" => "pack://application:,,,/Assets/PlatformIcons/bilibili.png",
        "twitch" => "pack://application:,,,/Assets/PlatformIcons/twitch.png",
        "xiaohongshu" => "pack://application:,,,/Assets/PlatformIcons/xiaohongshu.png",
        _ => "pack://application:,,,/Assets/Favicon.png",
    };

    [ObservableProperty]
    private string title = string.Empty;

    public string TitleDisplayText => Title;

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(TitleDisplayText));
    }

    [ObservableProperty]
    private string uid = string.Empty;

    public string UidDisplayText => "UID\uFF1A" + (string.IsNullOrWhiteSpace(Uid) ? ExtractRoomUid(RoomUrl) : Uid.Trim());

    partial void OnUidChanged(string value)
    {
        OnPropertyChanged(nameof(UidDisplayText));
    }

    [ObservableProperty]
    private string headers = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityDisplayName))]
    private string quality = string.Empty;

    public string QualityDisplayName => NormalizeQualityName(Quality);

    [ObservableProperty]
    private string resolution = string.Empty;

    [ObservableProperty]
    private string bitrate = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveIsToNotify))]
    private bool isToNotify = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveIsToRecord))]
    private bool isToRecord = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveIsToMonitor))]
    private bool isToMonitor = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveIsToNotify))]
    [NotifyPropertyChangedFor(nameof(EffectiveIsToRecord))]
    [NotifyPropertyChangedFor(nameof(EffectiveIsToMonitor))]
    [NotifyPropertyChangedFor(nameof(CanEditRoomSettings))]
    private bool isFollowGlobalSettings = true;

    public bool EffectiveIsToNotify => Configurations.IsToNotify.Get() && IsToNotify;

    public bool EffectiveIsToRecord => GlobalMonitor.GetEffectiveRoomRecord(RoomUrl, IsToRecord, IsFollowGlobalSettings);

    public bool EffectiveIsToMonitor => GlobalMonitor.GetEffectiveRoomMonitor(RoomUrl, IsToMonitor, IsFollowGlobalSettings);

    public bool CanEditRoomSettings => !IsFollowGlobalSettings;

    public string RecordFormatText => RoomRecordingSettings.Get(RoomUrl).RecordFormat;

    public string LiveStreamUrl => SelectLiveStreamUrl(RecordUrl, HlsUrl, FlvUrl);

    public string LiveStreamDisplayText => KeepUrlEdges(LiveStreamUrl, 88);

    partial void OnRecordUrlChanged(string value)
    {
        OnPropertyChanged(nameof(LiveStreamUrl));
        OnPropertyChanged(nameof(LiveStreamDisplayText));
    }

    partial void OnHlsUrlChanged(string value)
    {
        OnPropertyChanged(nameof(LiveStreamUrl));
        OnPropertyChanged(nameof(LiveStreamDisplayText));
    }

    partial void OnFlvUrlChanged(string value)
    {
        OnPropertyChanged(nameof(LiveStreamUrl));
        OnPropertyChanged(nameof(LiveStreamDisplayText));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StreamStatusText))]
    private StreamStatus streamStatus = default;

    public string StreamStatusText => StreamStatus switch
    {
        StreamStatus.Initialized => "StreamStatusOfInitialized".Tr(),
        StreamStatus.Disabled => "StreamStatusOfDisabled".Tr(),
        StreamStatus.NotStreaming => "StreamStatusOfNotStreaming".Tr(),
        StreamStatus.Streaming => "StreamStatusOfStreaming".Tr(),
        _ => "StreamStatusOfUnknown".Tr(),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordStatusText))]
    private RecordStatus recordStatus = default;

    public string RecordStatusText => RecordStatus switch
    {
        RecordStatus.Initialized => "RecordStatusOfInitialized".Tr(),
        RecordStatus.Disabled => "RecordStatusOfDisabled".Tr(),
        RecordStatus.NotRecording => "RecordStatusOfNotRecording".Tr(),
        RecordStatus.Recording => "RecordStatusOfRecording".Tr() + " " + Duration,
#pragma warning disable CS0618 // Type or member is obsolete
        RecordStatus.Error => "RecordStatusOfError".Tr(),
#pragma warning restore CS0618 // Type or member is obsolete
        _ => "RecordStatusOfUnknown".Tr(),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    public DateTime startTime = DateTime.MinValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    public DateTime endTime = DateTime.MinValue;

    public string Duration
    {
        get
        {
            if (StartTime != DateTime.MinValue)
            {
                if (EndTime != DateTime.MinValue)
                {
                    return (EndTime - StartTime).ToTimeCodeString();
                }
                return (DateTime.Now - StartTime).ToTimeCodeString();
            }
            return string.Empty;
        }
    }

    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(StreamStatusText));
        OnPropertyChanged(nameof(RecordStatusText));
        OnPropertyChanged(nameof(RecordFormatText));
        OnPropertyChanged(nameof(EffectiveIsToNotify));
        OnPropertyChanged(nameof(EffectiveIsToRecord));
        OnPropertyChanged(nameof(EffectiveIsToMonitor));
        OnPropertyChanged(nameof(CanEditRoomSettings));
    }

    public void RefreshDuration()
    {
        if (RecordStatus == RecordStatus.Recording)
        {
            OnPropertyChanged(nameof(RecordStatusText));
            OnPropertyChanged(nameof(Duration));
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (GlobalMonitor.RoomStatus.TryGetValue(RoomUrl, out RoomStatus? roomStatus))
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
            await Player.PreviewAsync(RoomUrl, NickName, RecordUrl, HlsUrl, FlvUrl, Headers, Title);
        }
    }

    [RelayCommand]
    private async Task GotoRoomUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri(RoomUrl));
    }

    [RelayCommand]
    private void CopyRoomUrl()
    {
        CopyToClipboard(RoomUrl);
    }

    [RelayCommand]
    private void CopyLiveStream()
    {
        CopyToClipboard(LiveStreamUrl);
    }

    [RelayCommand]
    private void ToggleMonitor()
    {
        bool enabled = !GlobalMonitor.GetEffectiveRoomMonitor(RoomUrl, IsToMonitor, IsFollowGlobalSettings);
        if (IsFollowGlobalSettings)
        {
            GlobalMonitor.SetTemporaryRoomMonitor(RoomUrl, enabled);
        }
        else
        {
            IsToMonitor = enabled;
        }
        OnPropertyChanged(nameof(EffectiveIsToMonitor));
    }

    [RelayCommand]
    private void ToggleRecord()
    {
        bool enabled = !GlobalMonitor.GetEffectiveRoomRecord(RoomUrl, IsToRecord, IsFollowGlobalSettings);
        if (IsFollowGlobalSettings)
        {
            GlobalMonitor.SetTemporaryRoomRecord(RoomUrl, enabled);
        }
        else
        {
            IsToRecord = enabled;
        }
        OnPropertyChanged(nameof(EffectiveIsToRecord));
    }

    [RelayCommand]
    private async Task OpenLocalSettingsAsync()
    {
        ContentDialog dialog = new()
        {
            Title = "LocalSettings".Tr(),
            CornerRadius = new CornerRadius(8),
            Content = new System.Windows.Controls.Grid()
            {
                MinWidth = 360,
                MinHeight = 180,
            },
            CloseButtonText = "ButtonOfCancel".Tr(),
            PrimaryButtonText = "ButtonOfSave".Tr(),
            DefaultButton = ContentDialogButton.Primary,
        };

        using DialogBlurScope blurScope = new();
        _ = await dialog.ShowAsync();
    }

    private static void CopyToClipboard(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        System.Windows.Clipboard.SetText(value);
        Toast.Success("Copied".Tr());
    }

    private static string SelectLiveStreamUrl(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string MiddleEllipsis(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        int keep = Math.Max(8, (maxLength - 3) / 2);
        return $"{value[..keep]}...{value[^keep..]}";
    }

    private static string KeepUrlEdges(string value, int maxLength)
    {
        return MiddleEllipsis(value, maxLength).Replace("-", "\u2011").Replace("/", "/\u2060");
    }

    private static string EndEllipsis(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(1, maxLength - 3)]}...";
    }

    private static string NormalizePlatformName(string value)
    {
        return PlatformKey(value) switch
        {
            "douyin" => "\u6296\u97f3",
            "tiktok" => "TikTok",
            "kuaishou" => "\u5feb\u624b",
            "huya" => "\u864e\u7259",
            "douyu" => "\u6597\u9c7c",
            "bilibili" => "B\u7ad9",
            "twitch" => "Twitch",
            "xiaohongshu" => "\u5c0f\u7ea2\u4e66",
            _ => value.Trim(),
        };
    }

    private static string NormalizeQualityName(string value)
    {
        string quality = value.Trim();

        return quality.ToUpperInvariant() switch
        {
            "OD" => "\u539f\u753b",
            "BD" => "\u84dd\u5149",
            "UHD" => "\u8d85\u6e05",
            "HD" => "\u9ad8\u6e05",
            "SD" => "\u6807\u6e05",
            "LD" => "\u6d41\u7545",
            _ => quality,
        };
    }

    private static string ExtractRoomUid(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return value;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.LastOrDefault() ?? value;
    }

    private static string PlatformKey(string value)
    {
        string platform = value.Trim();

        if (platform.Equals("douyin", StringComparison.OrdinalIgnoreCase) ||
            platform.Contains("\u6296\u97f3", StringComparison.OrdinalIgnoreCase))
        {
            return "douyin";
        }

        if (platform.Equals("tiktok", StringComparison.OrdinalIgnoreCase))
        {
            return "tiktok";
        }

        if (platform.Equals("youtube", StringComparison.OrdinalIgnoreCase))
        {
            return "youtube";
        }

        if (platform.Equals("twitch", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("twitchtv", StringComparison.OrdinalIgnoreCase))
        {
            return "twitch";
        }

        if (platform.Equals("kuaishou", StringComparison.OrdinalIgnoreCase) ||
            platform.Contains("\u5feb\u624b", StringComparison.OrdinalIgnoreCase))
        {
            return "kuaishou";
        }

        if (platform.Equals("huya", StringComparison.OrdinalIgnoreCase) ||
            platform.Contains("\u864e\u7259", StringComparison.OrdinalIgnoreCase))
        {
            return "huya";
        }

        if (platform.Equals("douyu", StringComparison.OrdinalIgnoreCase) ||
            platform.Contains("\u6597\u9c7c", StringComparison.OrdinalIgnoreCase))
        {
            return "douyu";
        }

        if (platform.Equals("bilibili", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("bili", StringComparison.OrdinalIgnoreCase) ||
            platform.Contains("\u54d4\u54e9", StringComparison.OrdinalIgnoreCase))
        {
            return "bilibili";
        }

        if (platform.Equals("xiaohongshu", StringComparison.OrdinalIgnoreCase) ||
            platform.Contains("\u5c0f\u7ea2\u4e66", StringComparison.OrdinalIgnoreCase))
        {
            return "xiaohongshu";
        }

        string compact = platform
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(")", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\uff08", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\uff09", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        string[] aliases =
        [
            "yy",
            "bigo",
            "blued",
            "soop",
            "afreecatv",
            "soop\u539fafreecatv",
            "\u7f51\u6613cc",
            "\u5343\u5ea6\u70ed\u64ad",
            "pandatv",
            "\u732b\u8033fm",
            "look\u76f4\u64ad",
            "winktv",
            "ttinglive",
            "flextv",
            "ttinglive\u539fflextv",
            "popkontv",
            "twitcasting",
            "\u767e\u5ea6\u76f4\u64ad",
            "\u5fae\u535a\u76f4\u64ad",
            "\u9177\u72d7\u76f4\u64ad",
            "liveme",
            "\u82b1\u6912\u76f4\u64ad",
            "\u6d41\u661f\u76f4\u64ad",
            "showroom",
            "acfun",
            "\u6620\u5ba2\u76f4\u64ad",
            "\u97f3\u64ad\u76f4\u64ad",
            "\u77e5\u4e4e\u76f4\u64ad",
            "chzzk",
            "\u55e8\u79c0\u76f4\u64ad",
            "vv\u661f\u7403\u76f4\u64ad",
            "17live",
            "\u6d6alive",
            "\u7545\u804a\u76f4\u64ad",
            "\u98d8\u98d8\u76f4\u64ad",
            "\u516d\u95f4\u623f\u76f4\u64ad",
            "\u4e50\u55e8\u76f4\u64ad",
            "\u82b1\u732b\u76f4\u64ad",
            "shopee",
            "\u6dd8\u5b9d",
            "\u4eac\u4e1c",
            "faceit",
            "\u54aa\u5495",
            "\u8fde\u63a5\u76f4\u64ad",
            "\u6765\u79c0\u76f4\u64ad",
            "picarto",
        ];

        foreach (string alias in aliases)
        {
            if (compact.Contains(alias, StringComparison.OrdinalIgnoreCase))
            {
                return "favicon";
            }
        }

        return string.Empty;
    }
}
public sealed class CommandEventArgs(string command) : EventArgs
{
    public string Command { get; } = command;
}

file static class TimeSpanExtension
{
    public static string ToTimeCodeString(this TimeSpan timeSpan)
    {
        timeSpan = new TimeSpan(timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);

        if (timeSpan.TotalHours < 1)
        {
            return timeSpan.ToString(@"mm\:ss");
        }
        else
        {
            return timeSpan.ToString(@"h\:mm\:ss");
        }
    }
}


