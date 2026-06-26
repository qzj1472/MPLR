namespace MPLR.Core;

internal static class PlatformDetector
{
    public static string DetectFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        string value = url.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return string.Empty;
        }

        string host = uri.Host.Trim().ToLowerInvariant();

        if (host.EndsWith("douyin.com", StringComparison.Ordinal) ||
            host.EndsWith("iesdouyin.com", StringComparison.Ordinal))
        {
            return "Douyin";
        }

        if (host.EndsWith("tiktok.com", StringComparison.Ordinal))
        {
            return "tiktok";
        }

        if (host.EndsWith("bilibili.com", StringComparison.Ordinal) ||
            host.EndsWith("b23.tv", StringComparison.Ordinal))
        {
            return "Bilibili";
        }

        if (host.EndsWith("huya.com", StringComparison.Ordinal))
        {
            return "Huya";
        }

        if (host.EndsWith("douyu.com", StringComparison.Ordinal))
        {
            return "Douyu";
        }

        if (host.EndsWith("kuaishou.com", StringComparison.Ordinal) ||
            host.EndsWith("chenzhongtech.com", StringComparison.Ordinal))
        {
            return "Kuaishou";
        }

        if (host.EndsWith("twitch.tv", StringComparison.Ordinal))
        {
            return "twitch";
        }

        if (host.EndsWith("xiaohongshu.com", StringComparison.Ordinal) ||
            host.EndsWith("xhslink.com", StringComparison.Ordinal))
        {
            return "Xiaohongshu";
        }

        if (host.EndsWith("youtube.com", StringComparison.Ordinal) ||
            host.EndsWith("youtu.be", StringComparison.Ordinal))
        {
            return "YouTube";
        }

        if (host.EndsWith("cc.163.com", StringComparison.Ordinal))
        {
            return "NetEase CC";
        }

        if (host.EndsWith("yy.com", StringComparison.Ordinal))
        {
            return "YY";
        }

        if (host.EndsWith("afreecatv.com", StringComparison.Ordinal) ||
            host.EndsWith("sooplive.co.kr", StringComparison.Ordinal))
        {
            return "SOOP";
        }

        if (host.EndsWith("chzzk.naver.com", StringComparison.Ordinal))
        {
            return "CHZZK";
        }

        if (host.EndsWith("pandalive.co.kr", StringComparison.Ordinal))
        {
            return "PandaTV";
        }

        if (host.EndsWith("showroom-live.com", StringComparison.Ordinal))
        {
            return "SHOWROOM";
        }

        if (host.EndsWith("acfun.cn", StringComparison.Ordinal))
        {
            return "AcFun";
        }

        if (host.EndsWith("shopee.com", StringComparison.Ordinal) ||
            host.EndsWith("shp.ee", StringComparison.Ordinal))
        {
            return "Shopee";
        }

        if (host.EndsWith("taobao.com", StringComparison.Ordinal) ||
            host.EndsWith("tmall.com", StringComparison.Ordinal))
        {
            return "Taobao";
        }

        if (host.EndsWith("jd.com", StringComparison.Ordinal))
        {
            return "JD";
        }

        return string.Empty;
    }
}
