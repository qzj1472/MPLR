using System.Reflection;

namespace MPLR;

internal class AppConfig
{
    public static string PackName => "MPLR";
    public static IReadOnlyList<string> LegacyPackNames => ["MultiPlatformLiveRecorder", "TiktokLiveRec"];
    public static IReadOnlyList<string> LegacyDisplayNames => ["多平台录播"];
    public static string DisplayName => "MPLR";
    public static string EnglishName => "MPLR";
    public static string LocalizedDisplayName => "MPLR";
    public static string StableUpdateChannel => "win";
    public static string BetaUpdateChannel => "win-beta";
    public static string BuildChannel => NormalizeReleaseChannel(GetAssemblyMetadata("ReleaseChannel"));
    public static bool IsBetaBuild => BuildChannel == "beta";
    public static string BuildChannelDisplayName => IsBetaBuild ? "Beta 通道" : "稳定版通道";
    public static string Version => IsBetaBuild ? $"v{ProductVersion} Beta" : $"v{ProductVersion}";

    private static string ProductVersion =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        typeof(App).Assembly.GetName().Version!.ToString(3);

    private static string? GetAssemblyMetadata(string key)
    {
        return typeof(App).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string NormalizeReleaseChannel(string? value)
    {
        return string.Equals(value, "beta", StringComparison.OrdinalIgnoreCase) ? "beta" : "stable";
    }
}
