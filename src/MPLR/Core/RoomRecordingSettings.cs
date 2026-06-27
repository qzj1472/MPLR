namespace MPLR.Core;

public sealed record RoomRecordingOptions
{
    public string RecordFormat { get; init; } = "TS/FLV";

    public string StreamQuality { get; init; } = "OD";

    public bool IsRemoveTs { get; init; }

    public bool IsToSegment { get; init; }

    public int SegmentTime { get; init; } = 1800;

    public int SegmentTimeUnit { get; init; } = -1;

    public int RoutineInterval { get; init; } = 3000;

    public int RoutineScheduleMode { get; init; }

    public string RoutineScheduleDays { get; init; } = "1,2,3,4,5,6,0";

    public int RoutineScheduleStartHour { get; init; }

    public int RoutineScheduleStartMinute { get; init; }

    public int RoutineScheduleEndHour { get; init; } = 23;

    public int RoutineScheduleEndMinute { get; init; } = 59;

    public string SaveFolder { get; init; } = string.Empty;

    public int SaveFolderPathLevel { get; init; }

    public int SaveFileNameRule { get; init; }

    public string SaveFileNameCustomRule { get; init; } = "{主播名}_{录制时间}";
}

internal static class RoomRecordingSettings
{
    public static RoomRecordingOptions GetGlobal()
    {
        return new RoomRecordingOptions
        {
            RecordFormat = NormalizeRecordFormat(Configurations.RecordFormat.Get()),
            StreamQuality = NormalizeStreamQuality(Configurations.StreamQuality.Get()),
            IsRemoveTs = Configurations.IsRemoveTs.Get(),
            IsToSegment = Configurations.IsToSegment.Get(),
            SegmentTime = Math.Max(1, Configurations.SegmentTime.Get()),
            SegmentTimeUnit = Configurations.SegmentTimeUnit.Get(),
            RoutineInterval = Math.Max(500, Configurations.RoutineInterval.Get()),
            RoutineScheduleMode = Math.Clamp(Configurations.RoutineScheduleMode.Get(), 0, 1),
            RoutineScheduleDays = NormalizeScheduleDays(Configurations.RoutineScheduleDays.Get()),
            RoutineScheduleStartHour = Math.Clamp(Configurations.RoutineScheduleStartHour.Get(), 0, 23),
            RoutineScheduleStartMinute = Math.Clamp(Configurations.RoutineScheduleStartMinute.Get(), 0, 59),
            RoutineScheduleEndHour = Math.Clamp(Configurations.RoutineScheduleEndHour.Get(), 0, 23),
            RoutineScheduleEndMinute = Math.Clamp(Configurations.RoutineScheduleEndMinute.Get(), 0, 59),
            SaveFolder = Configurations.SaveFolder.Get() ?? string.Empty,
            SaveFolderPathLevel = Math.Clamp(Configurations.SaveFolderPathLevel.Get(), 0, 1),
            SaveFileNameRule = Math.Clamp(Configurations.SaveFileNameRule.Get(), 0, 4),
            SaveFileNameCustomRule = NormalizeCustomRule(Configurations.SaveFileNameCustomRule.Get()),
        };
    }

    public static RoomRecordingOptions Get(Room room)
    {
        RoomRecordingOptions global = GetGlobal();
        if (room.IsFollowGlobalSettings)
        {
            return global;
        }

        return new RoomRecordingOptions
        {
            RecordFormat = NormalizeRecordFormat(room.RecordFormat, global.RecordFormat),
            StreamQuality = NormalizeStreamQuality(room.StreamQuality, global.StreamQuality),
            IsRemoveTs = room.IsRemoveTs ?? global.IsRemoveTs,
            IsToSegment = room.IsToSegment ?? global.IsToSegment,
            SegmentTime = Math.Max(1, room.SegmentTime ?? global.SegmentTime),
            SegmentTimeUnit = room.SegmentTimeUnit ?? global.SegmentTimeUnit,
            RoutineInterval = Math.Max(500, room.RoutineInterval ?? global.RoutineInterval),
            RoutineScheduleMode = Math.Clamp(room.RoutineScheduleMode ?? global.RoutineScheduleMode, 0, 1),
            RoutineScheduleDays = NormalizeScheduleDays(room.RoutineScheduleDays, global.RoutineScheduleDays),
            RoutineScheduleStartHour = Math.Clamp(room.RoutineScheduleStartHour ?? global.RoutineScheduleStartHour, 0, 23),
            RoutineScheduleStartMinute = Math.Clamp(room.RoutineScheduleStartMinute ?? global.RoutineScheduleStartMinute, 0, 59),
            RoutineScheduleEndHour = Math.Clamp(room.RoutineScheduleEndHour ?? global.RoutineScheduleEndHour, 0, 23),
            RoutineScheduleEndMinute = Math.Clamp(room.RoutineScheduleEndMinute ?? global.RoutineScheduleEndMinute, 0, 59),
            SaveFolder = room.SaveFolder ?? global.SaveFolder,
            SaveFolderPathLevel = Math.Clamp(room.SaveFolderPathLevel ?? global.SaveFolderPathLevel, 0, 1),
            SaveFileNameRule = Math.Clamp(room.SaveFileNameRule ?? global.SaveFileNameRule, 0, 4),
            SaveFileNameCustomRule = NormalizeCustomRule(room.SaveFileNameCustomRule, global.SaveFileNameCustomRule),
        };
    }

    public static RoomRecordingOptions Get(string roomUrl)
    {
        Room? room = Configurations.Rooms.Get()
            .FirstOrDefault(item => string.Equals(item.RoomUrl, roomUrl, StringComparison.OrdinalIgnoreCase));
        return room == null ? GetGlobal() : Get(room);
    }

    public static void Apply(Room room, RoomRecordingOptions settings)
    {
        room.RecordFormat = NormalizeRecordFormat(settings.RecordFormat);
        room.StreamQuality = NormalizeStreamQuality(settings.StreamQuality);
        room.IsRemoveTs = settings.IsRemoveTs;
        room.IsToSegment = settings.IsToSegment;
        room.SegmentTime = Math.Max(1, settings.SegmentTime);
        room.SegmentTimeUnit = settings.SegmentTimeUnit;
        room.RoutineInterval = Math.Max(500, settings.RoutineInterval);
        room.RoutineScheduleMode = Math.Clamp(settings.RoutineScheduleMode, 0, 1);
        room.RoutineScheduleDays = NormalizeScheduleDays(settings.RoutineScheduleDays);
        room.RoutineScheduleStartHour = Math.Clamp(settings.RoutineScheduleStartHour, 0, 23);
        room.RoutineScheduleStartMinute = Math.Clamp(settings.RoutineScheduleStartMinute, 0, 59);
        room.RoutineScheduleEndHour = Math.Clamp(settings.RoutineScheduleEndHour, 0, 23);
        room.RoutineScheduleEndMinute = Math.Clamp(settings.RoutineScheduleEndMinute, 0, 59);
        room.SaveFolder = settings.SaveFolder ?? string.Empty;
        room.SaveFolderPathLevel = Math.Clamp(settings.SaveFolderPathLevel, 0, 1);
        room.SaveFileNameRule = Math.Clamp(settings.SaveFileNameRule, 0, 4);
        room.SaveFileNameCustomRule = NormalizeCustomRule(settings.SaveFileNameCustomRule);
    }

    private static string NormalizeRecordFormat(string? value, string fallback = "TS/FLV")
    {
        return value switch
        {
            "TS/FLV -> MP4" => "TS/FLV -> MP4",
            "TS/FLV -> MKV" => "TS/FLV -> MKV",
            "TS/FLV" => "TS/FLV",
            _ => fallback,
        };
    }

    private static string NormalizeStreamQuality(string? value, string fallback = "OD")
    {
        string quality = (value ?? string.Empty).Trim().ToUpperInvariant();
        return quality is "OD" or "BD" or "UHD" or "HD" or "SD" or "LD" ? quality : fallback;
    }

    private static string NormalizeScheduleDays(string? value, string fallback = "1,2,3,4,5,6,0")
    {
        List<int> days = [];
        foreach (string item in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(item, out int day) && day is >= 0 and <= 6 && !days.Contains(day))
            {
                days.Add(day);
            }
        }

        return days.Count == 0 ? fallback : string.Join(",", days);
    }

    private static string NormalizeCustomRule(string? value, string fallback = "{主播名}_{录制时间}")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
