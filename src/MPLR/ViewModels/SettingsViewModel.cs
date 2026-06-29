using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComputedConverters;
using Fischless.Configuration;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using MPLR.Core;
using MPLR.Extensions;
using Vanara.PInvoke;
using Windows.Storage;
using Windows.System;
using WindowsAPICodePack.Dialogs;
using Wpf.Ui.Appearance;
using Wpf.Ui.Violeta.Appearance;
using Wpf.Ui.Violeta.Controls;
using Wpf.Ui.Violeta.Resources;
using YamlDotNet.Core;

namespace MPLR.ViewModels;

[ObservableObject]
public partial class SettingsViewModel : ReactiveObject
{
    public static event EventHandler<int>? DisplayScaleChanged;

    public string RoutineIntervalSecondsText => RoutineIntervalUnitHelper.GetUnitText(RoutineIntervalUnitHelper.Seconds);

    public string RoutineIntervalMinutesText => RoutineIntervalUnitHelper.GetUnitText(RoutineIntervalUnitHelper.Minutes);

    public string SegmentTimeSecondsText => SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Seconds);

    public string SegmentTimeMinutesText => SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Minutes);

    public string SegmentTimeHoursText => SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Hours);

    public string SegmentTimeMegabytesText => SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Megabytes);

    public string SegmentTimeGigabytesText => SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Gigabytes);

    public string SegmentTimeValueLabel => SegmentTimeUnitHelper.GetValueLabel(SegmentTimeUnitIndex);

    private enum LanguageIndexEnum
    {
        Auto,
        ChineseSimplified,
        ChineseTraditional,
        English,
        Japanese,
    }

    private enum UpdateChannelIndexEnum
    {
        Auto,
        Stable,
        Beta,
    }

    [ObservableProperty]
    private int languageIndex = Configurations.Language.Get() switch
    {
        "zh" or "zh-Hans" => (int)LanguageIndexEnum.ChineseSimplified,
        "zh-Hant" => (int)LanguageIndexEnum.ChineseTraditional,
        "en" => (int)LanguageIndexEnum.English,
        "ja" => (int)LanguageIndexEnum.Japanese,
        _ => (int)LanguageIndexEnum.Auto,
    };

    partial void OnLanguageIndexChanged(int value)
    {
        string language = value switch
        {
            (int)LanguageIndexEnum.ChineseSimplified => "zh-Hans",
            (int)LanguageIndexEnum.ChineseTraditional => "zh-Hant",
            (int)LanguageIndexEnum.English => "en",
            (int)LanguageIndexEnum.Japanese => "ja",
            _ => string.Empty,
        };

        Locale.Culture = value switch
        {
            (int)LanguageIndexEnum.Auto => new CultureInfo(Interop.GetUserDefaultLocaleName()),
            _ => new CultureInfo(language),
        };

        Configurations.Language.Set(language);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private int themeIndex = Configurations.Theme.Get() switch
    {
        nameof(ApplicationTheme.Dark) => 1,
        nameof(ApplicationTheme.Light) => 2,
        _ => 0,
    };

    partial void OnThemeIndexChanged(int value)
    {
        ApplicationTheme theme = value switch
        {
            1 => ApplicationTheme.Dark,
            2 => ApplicationTheme.Light,
            _ => ApplicationTheme.Unknown,
        };

        ThemeManager.Apply(theme);
        Configurations.Theme.Set(theme switch
        {
            ApplicationTheme.Light => nameof(ApplicationTheme.Light),
            ApplicationTheme.Dark => nameof(ApplicationTheme.Dark),
            _ => string.Empty,
        });
        ConfigurationManager.Save();
    }

    public ObservableCollection<PlatformCookieItem> PlatformCookieItems { get; } = [];

    [ObservableProperty]
    private int displayScale = Math.Clamp(Configurations.DisplayScale.Get(), 86, 114);

    partial void OnDisplayScaleChanged(int value)
    {
        int scale = Math.Clamp(value, 86, 114);

        if (scale != value)
        {
            DisplayScale = scale;
            return;
        }

        Configurations.DisplayScale.Set(scale);
        ConfigurationManager.Save();
        DisplayScaleChanged?.Invoke(this, scale);
    }

    [ObservableProperty]
    private bool isUseStatusTray = Configurations.IsUseStatusTray.Get();

    partial void OnIsUseStatusTrayChanged(bool value)
    {
        Configurations.IsUseStatusTray.Set(value);
        ConfigurationManager.Save();
        TrayIconManager.GetInstance().UpdateTrayIcon();
    }

    [ObservableProperty]
    private bool isSessionLogEnabled = AppUpdater.IsForcedUpdateEnabled || Configurations.IsSessionLogEnabled.Get();

    partial void OnIsSessionLogEnabledChanged(bool value)
    {
        if (!value && AppUpdater.IsForcedUpdateEnabled)
        {
            IsSessionLogEnabled = true;
            Toast.Information("Beta 版本或通道会强制开启日志，方便定位测试问题");
            return;
        }

        Configurations.IsSessionLogEnabled.Set(value);
        ConfigurationManager.Save();

        if (value)
        {
            AppSessionLogger.StartNow("session logging enabled");
        }
        else
        {
            AppSessionLogger.Stop("session logging disabled");
        }
    }

    [ObservableProperty]
    private bool isDeveloperModeEnabled = DeveloperModeManager.IsEnabled;

    partial void OnIsDeveloperModeEnabledChanged(bool value)
    {
        DeveloperModeManager.SetEnabled(value);
    }

    public bool IsSessionLogEditable => !AppUpdater.IsForcedUpdateEnabled;

    public string SessionLogStatus => IsSessionLogEditable
        ? "开启后每次启动生成一个日志文件，仅保留最近一周"
        : "Beta 版本或通道会强制开启日志，仅保留最近一周";

    public string UpdateChannelStatus => UpdateChannelIndex switch
    {
        (int)UpdateChannelIndexEnum.Stable when AppConfig.IsBetaBuild => "只检查正式稳定版，当前 Beta 包会强制自动切回稳定版",
        (int)UpdateChannelIndexEnum.Stable => "只检查正式稳定版，切换后会立即检查稳定版更新",
        (int)UpdateChannelIndexEnum.Beta => "会检查 GitHub 预发布 Beta 版，并强制自动更新",
        _ => $"跟随当前安装包（{AppConfig.BuildChannelDisplayName}）",
    };

    private int appliedUpdateChannelIndex = GetConfiguredUpdateChannelIndex();

    private bool isApplyingUpdateChannelIndex;

    public bool CanChangeUpdateChannel => !IsUpdateChannelChangeInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeUpdateChannel))]
    private bool isUpdateChannelChangeInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateChannelStatus))]
    private int updateChannelIndex = GetConfiguredUpdateChannelIndex();

    partial void OnUpdateChannelIndexChanged(int value)
    {
        if (isApplyingUpdateChannelIndex)
        {
            return;
        }

        int index = Math.Clamp(value, (int)UpdateChannelIndexEnum.Auto, (int)UpdateChannelIndexEnum.Beta);

        if (index != value)
        {
            SetUpdateChannelIndexSilently(index);
            return;
        }

        _ = ApplyUpdateChannelChangeAsync(index);
    }

    private async Task ApplyUpdateChannelChangeAsync(int index)
    {
        if (index == appliedUpdateChannelIndex)
        {
            return;
        }

        string channelName = GetUpdateChannelDisplayName(index);
        string message = index switch
        {
            (int)UpdateChannelIndexEnum.Beta => "切换到 Beta 通道后，会接收 GitHub 预发布版本，并强制开启日志和自动更新。应用会立即检查 Beta 版本，下载完成后需要重启安装。是否继续？",
            (int)UpdateChannelIndexEnum.Stable => "切换到稳定版通道后，会停止接收 Beta 预发布版本。应用会立即检查稳定版，当前如果是 Beta 包会自动安装稳定版来完成切换。是否继续？",
            _ => $"切换到跟随安装包后，将按当前安装包通道检查更新。当前安装包是 {AppConfig.BuildChannelDisplayName}，应用会立即检查对应版本。是否继续？",
        };

        try
        {
            System.Windows.MessageBoxResult result = await MessageBox.QuestionAsync(message);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                SetUpdateChannelIndexSilently(appliedUpdateChannelIndex);
                return;
            }

            IsUpdateChannelChangeInProgress = true;
            Configurations.UpdateChannel.Set(GetUpdateChannelValue(index));
            ConfigurationManager.Save();
            appliedUpdateChannelIndex = index;
            RefreshUpdatePolicyState();

            Toast.Information($"已切换到{channelName}，正在检查对应版本");
            await AppUpdater.CheckAsync(showNoUpdateMessage: true, forceInstall: true);
        }
        catch (Exception exception)
        {
            AppSessionLogger.WriteException(exception);
            Toast.Error($"切换更新通道失败：{exception.Message}");
            Configurations.UpdateChannel.Set(GetUpdateChannelValue(appliedUpdateChannelIndex));
            try
            {
                ConfigurationManager.Save();
            }
            catch (Exception saveException)
            {
                AppSessionLogger.WriteException(saveException);
            }
            SetUpdateChannelIndexSilently(appliedUpdateChannelIndex);
            RefreshUpdatePolicyState();
        }
        finally
        {
            IsUpdateChannelChangeInProgress = false;
        }
    }

    private void RefreshUpdatePolicyState()
    {
        OnPropertyChanged(nameof(UpdateChannelStatus));
        OnPropertyChanged(nameof(IsSessionLogEditable));
        OnPropertyChanged(nameof(SessionLogStatus));

        if (AppUpdater.IsForcedUpdateEnabled && !IsSessionLogEnabled)
        {
            IsSessionLogEnabled = true;
        }
    }

    private void SetUpdateChannelIndexSilently(int value)
    {
        isApplyingUpdateChannelIndex = true;
        try
        {
            UpdateChannelIndex = value;
        }
        finally
        {
            isApplyingUpdateChannelIndex = false;
        }
    }

    private static int GetConfiguredUpdateChannelIndex()
    {
        return (Configurations.UpdateChannel.Get() ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "stable" => (int)UpdateChannelIndexEnum.Stable,
            "beta" => (int)UpdateChannelIndexEnum.Beta,
            _ => (int)UpdateChannelIndexEnum.Auto,
        };
    }

    private static string GetUpdateChannelValue(int index)
    {
        return index switch
        {
            (int)UpdateChannelIndexEnum.Stable => "stable",
            (int)UpdateChannelIndexEnum.Beta => "beta",
            _ => "auto",
        };
    }

    private static string GetUpdateChannelDisplayName(int index)
    {
        return index switch
        {
            (int)UpdateChannelIndexEnum.Stable => "稳定版通道",
            (int)UpdateChannelIndexEnum.Beta => "Beta 通道",
            _ => "跟随安装包",
        };
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        Process.Start(new ProcessStartInfo()
        {
            FileName = AppPaths.LogsDirectory,
            UseShellExecute = true,
        });
    }

    public void ExportLogs(bool latest)
    {
        using CommonOpenFileDialog folderDialog = new()
        {
            IsFolderPicker = true,
            EnsurePathExists = true,
            Title = "选择日志导出目录",
        };

        if (folderDialog.ShowDialog() != CommonFileDialogResult.Ok)
        {
            return;
        }

        try
        {
            string archivePath = latest
                ? LogExporter.ExportLatest(folderDialog.FileName)
                : LogExporter.ExportAll(folderDialog.FileName);

            Toast.Success($"日志已导出：{archivePath}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Toast.Error($"日志导出失败：{e.Message}");
        }
    }

    [RelayCommand]
    private void CreateDesktopShortcut()
    {
        ShortcutHelper.CreateShortcutOnDesktop(
            shortcutName: AppConfig.DisplayName,
            targetPath: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName),
            arguments: null!,
            description: AppConfig.LocalizedDisplayName,
            iconLocation: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".exe"));

        Toast.Success("SuccOp".Tr());
    }

    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        using CommonOpenFileDialog dialog = new()
        {
            EnsureFileExists = true,
            IsFolderPicker = false,
            Title = "ImportConfig".Tr(),
        };

        dialog.Filters.Add(new CommonFileDialogFilter("YAML", "*.yaml;*.yml"));

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
        {
            return;
        }

        try
        {
            string backupPath = ConfigImporter.Import(dialog.FileName);
            AppSessionLogger.Write($"config imported from {dialog.FileName}; backup={backupPath}");
            Toast.Success("ImportConfigSuccess".Tr());
            _ = await MessageBox.InformationAsync("ImportConfigRestartHint".Tr(backupPath));
            AppSessionLogger.Write("application restarting after config import");
            AppSessionLogger.Stop("application restarting");
            RuntimeHelper.Restart(forced: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or YamlException)
        {
            AppSessionLogger.WriteException(e);
            Toast.Error("ImportConfigFailed".Tr(e.Message));
        }
    }

    [RelayCommand]
    private void ExportConfig()
    {
        using CommonSaveFileDialog dialog = new()
        {
            DefaultExtension = "yaml",
            DefaultFileName = $"config-{DateTime.Now:yyyyMMddHHmmss}.yaml",
            Title = "导出配置",
        };

        dialog.Filters.Add(new CommonFileDialogFilter("YAML", "*.yaml;*.yml"));

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
        {
            return;
        }

        try
        {
            ConfigImporter.Export(dialog.FileName);
            Toast.Success("配置已导出");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Toast.Error($"配置导出失败：{e.Message}");
        }
    }

    [RelayCommand]
    private async Task ResetConfigAsync()
    {
        if (MessageBox.Question("确定要重置配置文件吗？当前配置会先备份，重启应用后生效。") != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            string[] backupPaths = ConfigImporter.Reset();
            string backupText = backupPaths.Length == 0 ? "没有找到需要备份的配置文件。" : string.Join(Environment.NewLine, backupPaths);
            Toast.Success("配置已重置");
            _ = await MessageBox.InformationAsync($"配置已重置，应用将立即退出。配置备份：{Environment.NewLine}{backupText}");
            TrayIconManager.GetInstance().RequestShutdown(confirmRecording: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Toast.Error($"配置重置失败：{e.Message}");
        }
    }

    [ObservableProperty]
    private bool isToNotify = Configurations.IsToNotify.Get();

    partial void OnIsToNotifyChanged(bool value)
    {
        Configurations.IsToNotify.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private int notifySummaryIntervalMinutes = Configurations.NotifySummaryIntervalMinutes.Get();

    partial void OnNotifySummaryIntervalMinutesChanged(int value)
    {
        int next = Math.Clamp(value, 0, 1440);
        if (next != value)
        {
            NotifySummaryIntervalMinutes = next;
            return;
        }

        Configurations.NotifySummaryIntervalMinutes.Set(next);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToNotifyWithSystem = Configurations.IsToNotifyWithSystem.Get();

    partial void OnIsToNotifyWithSystemChanged(bool value)
    {
        Configurations.IsToNotifyWithSystem.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToNotifyWithMusic = Configurations.IsToNotifyWithMusic.Get();

    partial void OnIsToNotifyWithMusicChanged(bool value)
    {
        Configurations.IsToNotifyWithMusic.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private string? toNotifyWithMusicPath = Configurations.ToNotifyWithMusicPath.Get();

    partial void OnToNotifyWithMusicPathChanged(string? value)
    {
        Configurations.ToNotifyWithMusicPath.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToNotifyWithEmail = Configurations.IsToNotifyWithEmail.Get();

    partial void OnIsToNotifyWithEmailChanged(bool value)
    {
        Configurations.IsToNotifyWithEmail.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private string toNotifyWithEmailSmtp = Configurations.ToNotifyWithEmailSmtp.Get();

    partial void OnToNotifyWithEmailSmtpChanged(string value)
    {
        Configurations.ToNotifyWithEmailSmtp.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private string toNotifyWithEmailUserName = Configurations.ToNotifyWithEmailUserName.Get();

    partial void OnToNotifyWithEmailUserNameChanged(string value)
    {
        Configurations.ToNotifyWithEmailUserName.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private string toNotifyWithEmailPassword = Configurations.ToNotifyWithEmailPassword.Get();

    partial void OnToNotifyWithEmailPasswordChanged(string value)
    {
        Configurations.ToNotifyWithEmailPassword.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToNotifyGotoRoomUrl = Configurations.IsToNotifyGotoRoomUrl.Get();

    partial void OnIsToNotifyGotoRoomUrlChanged(bool value)
    {
        Configurations.IsToNotifyGotoRoomUrl.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToNotifyGotoRoomUrlAndMute = Configurations.IsToNotifyGotoRoomUrlAndMute.Get();

    partial void OnIsToNotifyGotoRoomUrlAndMuteChanged(bool value)
    {
        Configurations.IsToNotifyGotoRoomUrlAndMute.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToRecord = Configurations.IsToRecord.Get();

    partial void OnIsToRecordChanged(bool value)
    {
        Configurations.IsToRecord.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private double routineIntervalValue = RoutineIntervalUnitHelper.ToDisplayValue(
        Configurations.RoutineInterval.Get(),
        RoutineIntervalUnitHelper.GetPreferredUnitIndex(Configurations.RoutineInterval.Get()));

    [ObservableProperty]
    private int routineIntervalUnitIndex = RoutineIntervalUnitHelper.GetPreferredUnitIndex(Configurations.RoutineInterval.Get());

    private bool isUpdatingRoutineInterval;

    partial void OnRoutineIntervalValueChanged(double value)
    {
        if (isUpdatingRoutineInterval)
        {
            return;
        }

        ApplyRoutineInterval(value, RoutineIntervalUnitIndex);
    }

    partial void OnRoutineIntervalUnitIndexChanged(int value)
    {
        if (isUpdatingRoutineInterval)
        {
            return;
        }

        int milliseconds = RoutineIntervalUnitHelper.ToMilliseconds(RoutineIntervalValue, value);
        double displayValue = RoutineIntervalUnitHelper.ToDisplayValue(milliseconds, value);

        isUpdatingRoutineInterval = true;
        try
        {
            RoutineIntervalValue = displayValue;
        }
        finally
        {
            isUpdatingRoutineInterval = false;
        }

        ApplyRoutineInterval(displayValue, value);
    }

    private void ApplyRoutineInterval(double value, int unitIndex)
    {
        int milliseconds = RoutineIntervalUnitHelper.ToMilliseconds(value, unitIndex);
        GlobalMonitor.RoutinePeriodicWait.Period = TimeSpan.FromMilliseconds(milliseconds);
        Configurations.RoutineInterval.Set(milliseconds);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoutineScheduleCustom))]
    [NotifyPropertyChangedFor(nameof(IsRoutineScheduleAlways))]
    private int routineScheduleModeIndex = Math.Clamp(Configurations.RoutineScheduleMode.Get(), 0, 1);

    public bool IsRoutineScheduleCustom => RoutineScheduleModeIndex == 1;

    public bool IsRoutineScheduleAlways => RoutineScheduleModeIndex == 0;

    partial void OnRoutineScheduleModeIndexChanged(int value)
    {
        int mode = Math.Clamp(value, 0, 1);

        if (mode != value)
        {
            RoutineScheduleModeIndex = mode;
            return;
        }

        Configurations.RoutineScheduleMode.Set(mode);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool routineScheduleMonday = IsRoutineScheduleDayEnabled(DayOfWeek.Monday);

    [ObservableProperty]
    private bool routineScheduleTuesday = IsRoutineScheduleDayEnabled(DayOfWeek.Tuesday);

    [ObservableProperty]
    private bool routineScheduleWednesday = IsRoutineScheduleDayEnabled(DayOfWeek.Wednesday);

    [ObservableProperty]
    private bool routineScheduleThursday = IsRoutineScheduleDayEnabled(DayOfWeek.Thursday);

    [ObservableProperty]
    private bool routineScheduleFriday = IsRoutineScheduleDayEnabled(DayOfWeek.Friday);

    [ObservableProperty]
    private bool routineScheduleSaturday = IsRoutineScheduleDayEnabled(DayOfWeek.Saturday);

    [ObservableProperty]
    private bool routineScheduleSunday = IsRoutineScheduleDayEnabled(DayOfWeek.Sunday);

    partial void OnRoutineScheduleMondayChanged(bool value) => SaveRoutineScheduleDays();

    partial void OnRoutineScheduleTuesdayChanged(bool value) => SaveRoutineScheduleDays();

    partial void OnRoutineScheduleWednesdayChanged(bool value) => SaveRoutineScheduleDays();

    partial void OnRoutineScheduleThursdayChanged(bool value) => SaveRoutineScheduleDays();

    partial void OnRoutineScheduleFridayChanged(bool value) => SaveRoutineScheduleDays();

    partial void OnRoutineScheduleSaturdayChanged(bool value) => SaveRoutineScheduleDays();

    partial void OnRoutineScheduleSundayChanged(bool value) => SaveRoutineScheduleDays();

    [ObservableProperty]
    private int routineScheduleStartHour = Math.Clamp(Configurations.RoutineScheduleStartHour.Get(), 0, 23);

    [ObservableProperty]
    private int routineScheduleStartMinute = Math.Clamp(Configurations.RoutineScheduleStartMinute.Get(), 0, 59);

    [ObservableProperty]
    private int routineScheduleEndHour = Math.Clamp(Configurations.RoutineScheduleEndHour.Get(), 0, 23);

    [ObservableProperty]
    private int routineScheduleEndMinute = Math.Clamp(Configurations.RoutineScheduleEndMinute.Get(), 0, 59);

    partial void OnRoutineScheduleStartHourChanged(int value)
    {
        int next = Math.Clamp(value, 0, 23);

        if (next != value)
        {
            RoutineScheduleStartHour = next;
            return;
        }

        Configurations.RoutineScheduleStartHour.Set(next);
        ConfigurationManager.Save();
    }

    partial void OnRoutineScheduleStartMinuteChanged(int value)
    {
        int next = Math.Clamp(value, 0, 59);

        if (next != value)
        {
            RoutineScheduleStartMinute = next;
            return;
        }

        Configurations.RoutineScheduleStartMinute.Set(next);
        ConfigurationManager.Save();
    }

    partial void OnRoutineScheduleEndHourChanged(int value)
    {
        int next = Math.Clamp(value, 0, 23);

        if (next != value)
        {
            RoutineScheduleEndHour = next;
            return;
        }

        Configurations.RoutineScheduleEndHour.Set(next);
        ConfigurationManager.Save();
    }

    partial void OnRoutineScheduleEndMinuteChanged(int value)
    {
        int next = Math.Clamp(value, 0, 59);

        if (next != value)
        {
            RoutineScheduleEndMinute = next;
            return;
        }

        Configurations.RoutineScheduleEndMinute.Set(next);
        ConfigurationManager.Save();
    }

    private static bool IsRoutineScheduleDayEnabled(DayOfWeek day)
    {
        foreach (string item in (Configurations.RoutineScheduleDays.Get() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(item, out int value) && value == (int)day)
            {
                return true;
            }
        }

        return false;
    }

    private void SaveRoutineScheduleDays()
    {
        List<int> days = [];

        if (RoutineScheduleMonday)
        {
            days.Add((int)DayOfWeek.Monday);
        }

        if (RoutineScheduleTuesday)
        {
            days.Add((int)DayOfWeek.Tuesday);
        }

        if (RoutineScheduleWednesday)
        {
            days.Add((int)DayOfWeek.Wednesday);
        }

        if (RoutineScheduleThursday)
        {
            days.Add((int)DayOfWeek.Thursday);
        }

        if (RoutineScheduleFriday)
        {
            days.Add((int)DayOfWeek.Friday);
        }

        if (RoutineScheduleSaturday)
        {
            days.Add((int)DayOfWeek.Saturday);
        }

        if (RoutineScheduleSunday)
        {
            days.Add((int)DayOfWeek.Sunday);
        }

        Configurations.RoutineScheduleDays.Set(string.Join(",", days));
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private int recordFormatIndex = Configurations.RecordFormat.Get() switch
    {
        "TS/FLV -> MP4" => 1,
        "TS/FLV -> MKV" => 2,
        "TS/FLV" or _ => 0,
    };

    partial void OnRecordFormatIndexChanged(int value)
    {
        Configurations.RecordFormat.Set(value switch
        {
            1 => "TS/FLV -> MP4",
            2 => "TS/FLV -> MKV",
            0 or _ => "TS/FLV",
        });
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isRemoveTs = Configurations.IsRemoveTs.Get();

    partial void OnIsRemoveTsChanged(bool value)
    {
        Configurations.IsRemoveTs.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isToSegment = Configurations.IsToSegment.Get();

    partial void OnIsToSegmentChanged(bool value)
    {
        Configurations.IsToSegment.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private double segmentTimeValue = SegmentTimeUnitHelper.ToDisplayValue(Configurations.SegmentTime.Get(), GetInitialSegmentTimeUnitIndex());

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SegmentTimeValueLabel))]
    private int segmentTimeUnitIndex = GetInitialSegmentTimeUnitIndex();

    private bool isUpdatingSegmentTime;

    partial void OnSegmentTimeValueChanged(double value)
    {
        if (isUpdatingSegmentTime)
        {
            return;
        }

        ApplySegmentTime(value, SegmentTimeUnitIndex);
    }

    partial void OnSegmentTimeUnitIndexChanged(int value)
    {
        int unitIndex = Math.Clamp(value, SegmentTimeUnitHelper.Seconds, SegmentTimeUnitHelper.Gigabytes);

        if (unitIndex != value)
        {
            SegmentTimeUnitIndex = unitIndex;
            return;
        }

        double displayValue = SegmentTimeUnitHelper.IsSizeUnit(unitIndex)
            ? SegmentTimeValue
            : SegmentTimeUnitHelper.ToDisplayValue(Configurations.SegmentTime.Get(), unitIndex);

        isUpdatingSegmentTime = true;
        try
        {
            SegmentTimeValue = displayValue;
        }
        finally
        {
            isUpdatingSegmentTime = false;
        }

        Configurations.SegmentTimeUnit.Set(unitIndex);
        ConfigurationManager.Save();
    }

    private void ApplySegmentTime(double value, int unitIndex)
    {
        int seconds = SegmentTimeUnitHelper.ToSeconds(value, unitIndex);
        Configurations.SegmentTime.Set(seconds);
        Configurations.SegmentTimeUnit.Set(unitIndex);
        ConfigurationManager.Save();
    }

    private static int GetInitialSegmentTimeUnitIndex()
    {
        int configuredUnit = Configurations.SegmentTimeUnit.Get();

        return configuredUnit is >= SegmentTimeUnitHelper.Seconds and <= SegmentTimeUnitHelper.Gigabytes
            ? configuredUnit
            : SegmentTimeUnitHelper.GetPreferredUnitIndex(Configurations.SegmentTime.Get());
    }

    [ObservableProperty]
    private string saveFolder = Configurations.SaveFolder.Get();

    partial void OnSaveFolderChanged(string value)
    {
        Configurations.SaveFolder.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private int saveFolderPathLevelIndex = Math.Clamp(Configurations.SaveFolderPathLevel.Get(), 0, 1);

    partial void OnSaveFolderPathLevelIndexChanged(int value)
    {
        int next = Math.Clamp(value, 0, 1);

        if (next != value)
        {
            SaveFolderPathLevelIndex = next;
            return;
        }

        Configurations.SaveFolderPathLevel.Set(next);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveFileNameRuleCustom))]
    private int saveFileNameRuleIndex = Math.Clamp(Configurations.SaveFileNameRule.Get(), 0, 4);

    public bool IsSaveFileNameRuleCustom => SaveFileNameRuleIndex == 4;

    partial void OnSaveFileNameRuleIndexChanged(int value)
    {
        int next = Math.Clamp(value, 0, 4);

        if (next != value)
        {
            SaveFileNameRuleIndex = next;
            return;
        }

        Configurations.SaveFileNameRule.Set(next);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private string saveFileNameCustomRule = string.IsNullOrWhiteSpace(Configurations.SaveFileNameCustomRule.Get())
        ? "{主播名}_{录制时间}"
        : Configurations.SaveFileNameCustomRule.Get();

    partial void OnSaveFileNameCustomRuleChanged(string value)
    {
        Configurations.SaveFileNameCustomRule.Set(string.IsNullOrWhiteSpace(value) ? "{主播名}_{录制时间}" : value);
        ConfigurationManager.Save();
    }

    [RelayCommand]
    private void AppendSaveFileNameToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SaveFileNameCustomRule))
        {
            SaveFileNameCustomRule = token;
            return;
        }

        SaveFileNameCustomRule = SaveFileNameCustomRule.EndsWith('_')
            ? SaveFileNameCustomRule + token
            : SaveFileNameCustomRule + "_" + token;
    }

    [RelayCommand]
    private void DeleteSaveFileNameToken()
    {
        string[] tokens = ["{主播uid}", "{主播名}", "{录制时间}", "{分辨率}", "{平台}"];

        foreach (string token in tokens.OrderByDescending(static value => value.Length))
        {
            if (SaveFileNameCustomRule.EndsWith(token, StringComparison.Ordinal))
            {
                string value = SaveFileNameCustomRule[..^token.Length];
                SaveFileNameCustomRule = value.EndsWith('_') ? value[..^1] : value;
                return;
            }
        }

        if (SaveFileNameCustomRule.Length > 0)
        {
            SaveFileNameCustomRule = SaveFileNameCustomRule[..^1];
        }
    }

    [RelayCommand]
    private void ResetSaveFileNameRule()
    {
        SaveFileNameCustomRule = "{主播名}_{录制时间}";
    }

    [RelayCommand]
    private void SelectSaveFolder()
    {
        using CommonOpenFileDialog dialog = new()
        {
            IsFolderPicker = true,
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            SaveFolder = dialog.FileName;
        }
    }

    [RelayCommand]
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    private async Task OpenSaveFolderAsync()
    {
        // TODO: Implement for other platforms
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(
                SaveFolderHelper.GetSaveFolder(Configurations.SaveFolder.Get())
            )
        );
    }

    [ObservableProperty]
    private int playerIndex;

    partial void OnPlayerIndexChanged(int value)
    {
        PlayerIndex = 0;
        Configurations.Player.Set("embedded");
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isPlayerRect = Configurations.IsPlayerRect.Get();

    partial void OnIsPlayerRectChanged(bool value)
    {
        Configurations.IsPlayerRect.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isUseKeepAwake = Configurations.IsUseKeepAwake.Get();

    partial void OnIsUseKeepAwakeChanged(bool value)
    {
        if (value)
        {
            // Start keep awake
            _ = Kernel32.SetThreadExecutionState(Kernel32.EXECUTION_STATE.ES_CONTINUOUS | Kernel32.EXECUTION_STATE.ES_SYSTEM_REQUIRED | Kernel32.EXECUTION_STATE.ES_AWAYMODE_REQUIRED);
        }
        else
        {
            // Stop keep awake
            _ = Kernel32.SetThreadExecutionState(Kernel32.EXECUTION_STATE.ES_CONTINUOUS);
        }
        Configurations.IsUseKeepAwake.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isUseAutoShutdown = Configurations.IsUseAutoShutdown.Get();

    partial void OnIsUseAutoShutdownChanged(bool value)
    {
        Configurations.IsUseAutoShutdown.Set(value);
        ConfigurationManager.Save();
    }

    private static int GetAutoShutdownTimePart(int index, int max)
    {
        string[] parts = (Configurations.AutoShutdownTime.Get() ?? string.Empty).Split(':');
        if (parts.Length <= index)
        {
            return 0;
        }

        return Math.Clamp(parts[index].IntParse(fallback: 0), 0, max);
    }

    [ObservableProperty]
    private int autoShutdownTimeHour = GetAutoShutdownTimePart(0, 23);

    partial void OnAutoShutdownTimeHourChanged(int value)
    {
        int next = Math.Clamp(value, 0, 23);

        if (next != value)
        {
            AutoShutdownTimeHour = next;
            return;
        }

        Configurations.AutoShutdownTime.Set($"{next:D2}:{AutoShutdownTimeMinute:D2}");
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private int autoShutdownTimeMinute = GetAutoShutdownTimePart(1, 59);

    partial void OnAutoShutdownTimeMinuteChanged(int value)
    {
        int next = Math.Clamp(value, 0, 59);

        if (next != value)
        {
            AutoShutdownTimeMinute = next;
            return;
        }

        Configurations.AutoShutdownTime.Set($"{AutoShutdownTimeHour:D2}:{next:D2}");
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isAutoShutdownAfterTranscode = Configurations.IsAutoShutdownAfterTranscode.Get();

    partial void OnIsAutoShutdownAfterTranscodeChanged(bool value)
    {
        Configurations.IsAutoShutdownAfterTranscode.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private bool isUseProxy = Configurations.IsUseProxy.Get();

    partial void OnIsUseProxyChanged(bool value)
    {
        Configurations.IsUseProxy.Set(value);
        ConfigurationManager.Save();
    }

    [ObservableProperty]
    private string proxyUrl = Configurations.ProxyUrl.Get();

    partial void OnProxyUrlChanged(string value)
    {
        Configurations.ProxyUrl.Set(value);
        ConfigurationManager.Save();
    }

    [RelayCommand]
    private async Task CheckProxyUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(ProxyUrl))
        {
            Toast.Error("ProxyErrorOfEmptyUrl".Tr());
            return;
        }

        if (!ProxyUrl.Contains(':'))
        {
            Toast.Error("ProxyErrorOfMissHostOrPort".Tr());
            return;
        }

        string[] proxy = ProxyUrl.Split(':');

        if (proxy.Length < 2)
        {
            Toast.Error("ProxyErrorOfFormat".Tr());
            return;
        }

        if (!IPAddress.TryParse(proxy[0], out IPAddress? address))
        {
            Toast.Error("ProxyErrorOfHostFormatError".Tr());
            return;
        }

        if (!int.TryParse(proxy[1], out int port))
        {
            Toast.Error("ProxyErrorOfPortFormatError".Tr());
            return;
        }

        if (port <= 0 || port > short.MaxValue)
        {
            Toast.Error("ProxyErrorOfPortOutOfRange".Tr());
            return;
        }

        HttpClientHandler httpClientHandler = new()
        {
            Proxy = new WebProxy(address.ToString(), port),
            UseProxy = true
        };

        using HttpClient httpClient = new(httpClientHandler);

        try
        {
            HttpResponseMessage response = await httpClient.GetAsync("https://www.google.com");
            response.EnsureSuccessStatusCode();

            Toast.Success("ProxySuccOfStatusCode".Tr(response.StatusCode));
        }
        catch (HttpRequestException e)
        {
            Toast.Error("ProxyErrorOfExceptionMessage".Tr(e.Message));
        }
    }

    [RelayCommand]
    private async Task OpenHowToGetCookieChinaAsync()
    {
        string html = ResourcesProvider.GetString("pack://application:,,,/Assets/GETCOOKIE_DOUYIN.html");
        Directory.CreateDirectory(AppPaths.CacheDirectory);
        string filePath = Path.GetFullPath(Path.Combine(AppPaths.CacheDirectory, "GETCOOKIE_DOUYIN.html"));

        File.WriteAllText(filePath, html);

        // TODO: Implement for other platforms
        await Launcher.LaunchUriAsync(new Uri($"file://{filePath}"));
    }

    [ObservableProperty]
    private string platformCookies = Configurations.PlatformCookies.Get();

    partial void OnPlatformCookiesChanged(string value)
    {
        Configurations.PlatformCookies.Set(value);
        ConfigurationManager.Save();
    }

    public SettingsViewModel()
    {
        Locale.CultureChanged += LocaleCultureChanged;
        LoadPlatformCookieItems();
    }

    private void LocaleCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RoutineIntervalSecondsText));
        OnPropertyChanged(nameof(RoutineIntervalMinutesText));
        OnPropertyChanged(nameof(SegmentTimeSecondsText));
        OnPropertyChanged(nameof(SegmentTimeMinutesText));
        OnPropertyChanged(nameof(SegmentTimeHoursText));
    }

    private void LoadPlatformCookieItems()
    {
        Dictionary<string, string> cookies = ParsePlatformCookies(PlatformCookies);

        foreach ((string key, string displayName) in SupportedCookiePlatforms())
        {
            PlatformCookieItem item = new(key, displayName, cookies.TryGetValue(key, out string? value) ? value : string.Empty);
            item.PropertyChanged += PlatformCookieItemPropertyChanged;
            PlatformCookieItems.Add(item);
        }
    }

    private void PlatformCookieItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlatformCookieItem.Cookie))
        {
            return;
        }

        PlatformCookies = string.Join(
            Environment.NewLine,
            PlatformCookieItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Cookie))
                .Select(item => $"{item.Key}={item.Cookie.Trim()}"));
    }

    private static Dictionary<string, string> ParsePlatformCookies(string value)
    {
        Dictionary<string, string> cookies = new(StringComparer.OrdinalIgnoreCase);

        foreach (string line in (value ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                separator = line.IndexOf(':');
            }

            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string cookie = line[(separator + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(cookie))
            {
                cookies[key] = cookie;
            }
        }

        return cookies;
    }

    private static (string Key, string DisplayName)[] SupportedCookiePlatforms()
    {
        return
        [
            ("douyin", "抖音 Cookie"),
            ("tiktok", "TikTok Cookie"),
            ("bilibili", "哔哩哔哩 Cookie"),
            ("kuaishou", "快手 Cookie"),
            ("douyu", "斗鱼 Cookie"),
            ("huya", "虎牙 Cookie"),
            ("twitch", "Twitch Cookie"),
            ("youtube", "YouTube Cookie"),
            ("sooplive", "SOOP Cookie"),
            ("pandalive", "PandaLive Cookie"),
            ("winktv", "WinkTV Cookie"),
            ("flextv", "FlexTV Cookie"),
            ("popkontv", "PopkonTV Cookie"),
            ("17live", "17Live Cookie"),
            ("langlive", "浪Live Cookie"),
            ("showroom", "SHOWROOM Cookie"),
            ("chzzk", "CHZZK Cookie"),
            ("shopee", "Shopee Cookie"),
        ];
    }

    [ObservableProperty]
    private string userAgent = Configurations.UserAgent.Get();

    partial void OnUserAgentChanged(string value)
    {
        Configurations.UserAgent.Set(value);
        ConfigurationManager.Save();
    }
}

public partial class PlatformCookieItem(string key, string displayName, string cookie) : ObservableObject
{
    public string Key { get; } = key;

    public string DisplayName { get; } = displayName;

    [ObservableProperty]
    private string cookie = cookie;
}

file static class Extensions
{
    public static int IntParse(this string value, int fallback = default)
    {
        if (int.TryParse(value, out int output))
        {
            return output;
        }
        return fallback;
    }
}

