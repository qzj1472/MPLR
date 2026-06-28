namespace MPLR;

internal class AppConfig
{
    public static string PackName => "MPLR";
    public static IReadOnlyList<string> LegacyPackNames => ["MultiPlatformLiveRecorder", "TiktokLiveRec"];
    public static IReadOnlyList<string> LegacyDisplayNames => ["多平台录播"];
    public static string DisplayName => "MPLR";
    public static string EnglishName => "MPLR";
    public static string LocalizedDisplayName => "MPLR";
    public static string Version => $"v{typeof(App).Assembly.GetName().Version!.ToString(4)} 测试版";
}

