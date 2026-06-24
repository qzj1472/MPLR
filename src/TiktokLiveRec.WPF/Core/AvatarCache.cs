using Fischless.Configuration;
using System.Net.Http;
using System.Security.Cryptography;

namespace TiktokLiveRec.Core;

internal static class AvatarCache
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public static string GetCachedAvatarPath(string roomUrl)
    {
        string hash = HashRoomUrl(roomUrl);
        return Path.Combine(GetAvatarDirectory(), $"{hash}.avatar");
    }

    public static string GetCachedAvatarSource(string roomUrl)
    {
        string path = GetCachedAvatarPath(roomUrl);
        return File.Exists(path) ? path : string.Empty;
    }

    public static async Task<string> UpdateAsync(string roomUrl, string avatarUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomUrl) || string.IsNullOrWhiteSpace(avatarUrl))
        {
            return GetCachedAvatarSource(roomUrl);
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, avatarUrl);
            request.Headers.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(Configurations.UserAgent.Get()) ? "Mozilla/5.0" : Configurations.UserAgent.Get());
            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return GetCachedAvatarSource(roomUrl);
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            if (bytes.Length == 0)
            {
                return GetCachedAvatarSource(roomUrl);
            }

            string path = GetCachedAvatarPath(roomUrl);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (File.Exists(path))
            {
                byte[] existing = await File.ReadAllBytesAsync(path, cancellationToken);
                if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(bytes)))
                {
                    return path;
                }
            }

            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            return path;
        }
        catch
        {
            return GetCachedAvatarSource(roomUrl);
        }
    }

    private static string GetAvatarDirectory()
    {
        return Path.Combine(Path.GetDirectoryName(ConfigurationManager.FilePath) ?? AppContext.BaseDirectory, "avatars");
    }

    public static string HashRoomUrl(string roomUrl)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(roomUrl ?? string.Empty))).ToLowerInvariant();
    }
}
