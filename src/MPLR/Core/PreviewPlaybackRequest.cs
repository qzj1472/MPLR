namespace MPLR.Core;

public sealed record PreviewPlaybackRequest(
    IReadOnlyList<string> Urls,
    string Title,
    string Headers,
    string RoomUrl,
    string NickName);
