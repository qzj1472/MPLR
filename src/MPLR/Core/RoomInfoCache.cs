using Fischless.Configuration;
using MPLR.Models;
using MPLR.ViewModels;

namespace MPLR.Core;

internal static class RoomInfoCache
{
    public static void Apply(Room room, RoomStatusReactive target)
    {
        target.AvatarThumbUrl = room.AvatarThumbUrl ?? string.Empty;
        target.Platform = room.Platform ?? string.Empty;
        target.Title = room.Title ?? string.Empty;
        target.Uid = room.Uid ?? string.Empty;
        target.Quality = room.Quality ?? string.Empty;
        target.Resolution = room.Resolution ?? string.Empty;
        target.Bitrate = room.Bitrate ?? string.Empty;
        target.Headers = room.Headers ?? string.Empty;
        target.FlvUrl = string.Empty;
        target.HlsUrl = string.Empty;
        target.RecordUrl = string.Empty;
        target.AvatarLocalPath = AvatarCache.GetCachedAvatarSource(room.RoomUrl);
    }

    public static void Apply(RoomStatusReactive source, Room target)
    {
        bool changed = HasRoomInfoChanged(source, target);

        target.AvatarThumbUrl = source.AvatarThumbUrl ?? string.Empty;
        target.Platform = source.Platform ?? string.Empty;
        target.Title = source.Title ?? string.Empty;
        target.Uid = source.Uid ?? string.Empty;
        target.Quality = source.Quality ?? string.Empty;
        target.Resolution = source.Resolution ?? string.Empty;
        target.Bitrate = source.Bitrate ?? string.Empty;
        target.Headers = source.Headers ?? string.Empty;
        target.FlvUrl = string.Empty;
        target.HlsUrl = string.Empty;
        target.RecordUrl = string.Empty;

        if (changed)
        {
            target.LastInfoUpdatedAt = DateTime.Now;
        }
    }

    public static void Apply(RoomStatus source, Room target)
    {
        bool changed = HasRoomInfoChanged(source, target);

        target.AvatarThumbUrl = source.AvatarThumbUrl ?? string.Empty;
        target.Platform = source.Platform ?? string.Empty;
        target.Title = source.Title ?? string.Empty;
        target.Uid = source.Uid ?? string.Empty;
        target.Quality = source.Quality ?? string.Empty;
        target.Resolution = source.Resolution ?? string.Empty;
        target.Bitrate = source.Bitrate ?? string.Empty;
        target.Headers = source.Headers ?? string.Empty;
        target.FlvUrl = string.Empty;
        target.HlsUrl = string.Empty;
        target.RecordUrl = string.Empty;

        if (changed)
        {
            target.LastInfoUpdatedAt = DateTime.Now;
        }
    }

    public static void Save(RoomStatusReactive source)
    {
        if (string.IsNullOrWhiteSpace(source.RoomUrl))
        {
            return;
        }

        Room[] rooms = Configurations.Rooms.Get();
        Room? room = rooms.FirstOrDefault(item => string.Equals(item.RoomUrl, source.RoomUrl, StringComparison.OrdinalIgnoreCase));

        if (room == null)
        {
            return;
        }

        if (!HasRoomInfoChanged(source, room))
        {
            return;
        }

        Apply(source, room);
        Configurations.Rooms.Set(rooms);
        ConfigurationManager.Save();
    }

    private static bool HasRoomInfoChanged(RoomStatusReactive source, Room target)
    {
        return IsDifferent(source.AvatarThumbUrl, target.AvatarThumbUrl) ||
               IsDifferent(source.Platform, target.Platform) ||
               IsDifferent(source.Title, target.Title) ||
               IsDifferent(source.Uid, target.Uid) ||
               IsDifferent(source.Quality, target.Quality) ||
               IsDifferent(source.Resolution, target.Resolution) ||
               IsDifferent(source.Bitrate, target.Bitrate) ||
               IsDifferent(source.Headers, target.Headers) ||
               IsDifferent(string.Empty, target.FlvUrl) ||
               IsDifferent(string.Empty, target.HlsUrl) ||
               IsDifferent(string.Empty, target.RecordUrl);
    }

    private static bool HasRoomInfoChanged(RoomStatus source, Room target)
    {
        return IsDifferent(source.AvatarThumbUrl, target.AvatarThumbUrl) ||
               IsDifferent(source.Platform, target.Platform) ||
               IsDifferent(source.Title, target.Title) ||
               IsDifferent(source.Uid, target.Uid) ||
               IsDifferent(source.Quality, target.Quality) ||
               IsDifferent(source.Resolution, target.Resolution) ||
               IsDifferent(source.Bitrate, target.Bitrate) ||
               IsDifferent(source.Headers, target.Headers) ||
               IsDifferent(string.Empty, target.FlvUrl) ||
               IsDifferent(string.Empty, target.HlsUrl) ||
               IsDifferent(string.Empty, target.RecordUrl);
    }

    private static bool IsDifferent(string? source, string? target)
    {
        return !string.Equals(source ?? string.Empty, target ?? string.Empty, StringComparison.Ordinal);
    }
}

