namespace MPLR.Core;

internal static class Spider
{
    public static string? ParseUrl(string url)
    {
        string? normalizedUrl = ExternalStreamResolver.NormalizeUrl(url);

        if (!string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return normalizedUrl;
        }

        if (url.Contains("douyin"))
        {
            return DouyinSpider.Instance.Value.ParseUrl(url);
        }
        else if (url.Contains("tiktok"))
        {
            return TiktokSpider.Instance.Value.ParseUrl(url);
        }

        return null;
    }

    public static ISpiderResult? GetResult(string url)
    {
        ISpiderResult? externalResult = ExternalStreamResolver.GetResult(url);

        if (externalResult != null)
        {
            if (url.Contains("douyin") && string.IsNullOrWhiteSpace(externalResult.AvatarThumbUrl))
            {
                ISpiderResult douyinResult = DouyinSpider.Instance.Value.GetResult(url);
                FillMissingDouyinFields(externalResult, douyinResult);
            }

            return externalResult;
        }

        if (url.Contains("douyin"))
        {
            return DouyinSpider.Instance.Value.GetResult(url);
        }
        else if (url.Contains("tiktok"))
        {
            return TiktokSpider.Instance.Value.GetResult(url);
        }

        return null;
    }

    private static void FillMissingDouyinFields(ISpiderResult target, ISpiderResult source)
    {
        if (string.IsNullOrWhiteSpace(target.AvatarThumbUrl))
        {
            target.AvatarThumbUrl = source.AvatarThumbUrl;
        }

        if (string.IsNullOrWhiteSpace(target.Nickname))
        {
            target.Nickname = source.Nickname;
        }

        if (string.IsNullOrWhiteSpace(target.RoomUrl))
        {
            target.RoomUrl = source.RoomUrl;
        }

        if (target.IsLiveStreaming == null)
        {
            target.IsLiveStreaming = source.IsLiveStreaming;
        }
    }
}

public interface ISpider
{
    public ISpiderResult GetResult(string url);
}

public interface ISpiderResult
{
    public string? RoomUrl { get; set; }

    public bool? IsLiveStreaming { get; set; }

    public string? Nickname { get; set; }

    public string? AvatarThumbUrl { get; set; }

    public string? FlvUrl { get; set; }

    public string? HlsUrl { get; set; }

    public string? RecordUrl { get; set; }

    public string? Platform { get; set; }

    public string? Title { get; set; }

    public string? Quality { get; set; }

    public string? Uid { get; set; }

    public string? Resolution { get; set; }

    public string? Bitrate { get; set; }

    public string? Headers { get; set; }
}

