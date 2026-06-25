namespace MPLR.Core;

internal static class PlatformCookieStore
{
    public static string Get(string platform, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return fallback;
        }

        Dictionary<string, string> cookies = Parse(Configurations.PlatformCookies.Get());
        return cookies.TryGetValue(platform, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static Dictionary<string, string> Parse(string value)
    {
        Dictionary<string, string> cookies = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                separator = line.IndexOf(':');
            }

            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string cookie = line[(separator + 1)..].Trim();
            if (key.Length > 0 && cookie.Length > 0)
            {
                cookies[key] = cookie;
            }
        }

        return cookies;
    }
}

