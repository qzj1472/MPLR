using Fischless.Configuration;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace MPLR.Core;

internal static class AvatarCache
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public static string GetCachedAvatarPath(string roomUrl)
    {
        string hash = HashRoomUrl(NormalizeRoomUrlForCache(roomUrl));
        return Path.Combine(GetAvatarDirectory(), $"{hash}.avatar");
    }

    public static string GetCachedAvatarSource(string roomUrl)
    {
        string path = GetCachedAvatarPath(roomUrl);
        if (IsUsableAvatarFile(path))
        {
            return path;
        }

        string legacyPath = GetLegacyCachedAvatarPath(roomUrl);
        if (!IsUsableAvatarFile(legacyPath))
        {
            return string.Empty;
        }

        TryCopyAvatar(legacyPath, path);
        return IsUsableAvatarFile(path) ? path : legacyPath;
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

            if (IsUsableAvatarFile(path))
            {
                byte[] existing = await File.ReadAllBytesAsync(path, cancellationToken);
                if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(bytes)))
                {
                    return path;
                }
            }

            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            File.Move(tempPath, path, true);
            return path;
        }
        catch
        {
            return GetCachedAvatarSource(roomUrl);
        }
    }

    private static string GetAvatarDirectory()
    {
        return Path.Combine(AppPaths.CacheDirectory, "avatars");
    }

    public static string HashRoomUrl(string roomUrl)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(roomUrl ?? string.Empty))).ToLowerInvariant();
    }

    private static string GetLegacyCachedAvatarPath(string roomUrl)
    {
        string hash = HashRoomUrl(roomUrl ?? string.Empty);
        return Path.Combine(GetAvatarDirectory(), $"{hash}.avatar");
    }

    private static string NormalizeRoomUrlForCache(string? roomUrl)
    {
        if (string.IsNullOrWhiteSpace(roomUrl))
        {
            return string.Empty;
        }

        string value = roomUrl.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return value.ToLowerInvariant();
        }

        string host = uri.Host.ToLowerInvariant();
        string path = uri.AbsolutePath.Trim('/').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(path) ? host : $"{host}/{path}";
    }

    private static bool IsUsableAvatarFile(string path)
    {
        try
        {
            FileInfo info = new(path);
            return info.Exists && info.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCopyAvatar(string source, string target)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
        catch
        {
        }
    }
}
