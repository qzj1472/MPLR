namespace TiktokLiveRec;

internal class AppConfig
{
    public static string PackName => "MultiPlatformLiveRecorder";
    public static string LegacyPackName => "TiktokLiveRec";
    public static string DisplayName => "多平台录播";
    public static string EnglishName => "Multi-platform Live Recorder";
    public static string LocalizedDisplayName => Locale.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? DisplayName : EnglishName;
    public static string Version => $"v{typeof(App).Assembly.GetName().Version!.ToString(3)}";
}
