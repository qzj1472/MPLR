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

