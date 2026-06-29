using System.Diagnostics;
using System.Net.Http;

namespace MPLR.Core;

internal sealed record NetworkCapacityTestResult(
    double DownloadMbps,
    double RoomMbps,
    int TheoreticalRooms,
    int SafeRooms,
    long BytesRead,
    double DurationSeconds,
    string SourceUrl);

internal static class NetworkCapacityTester
{
    public const double DefaultRoomMbps = 11d;
    public const int DefaultDurationSeconds = 10;
    public const string DefaultTestUrl = "https://speed.cloudflare.com/__down?bytes=200000000";

    public static async Task<NetworkCapacityTestResult> TestAsync(string sourceUrl, double roomMbps, int durationSeconds, CancellationToken token = default)
    {
        string url = string.IsNullOrWhiteSpace(sourceUrl) ? DefaultTestUrl : sourceUrl.Trim();
        double normalizedRoomMbps = roomMbps > 0 ? roomMbps : DefaultRoomMbps;
        int normalizedDurationSeconds = Math.Clamp(durationSeconds, 3, 60);
        TimeSpan duration = TimeSpan.FromSeconds(normalizedDurationSeconds);
        long totalBytes = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();

        using HttpClient client = new()
        {
            Timeout = duration + TimeSpan.FromSeconds(15),
        };

        while (stopwatch.Elapsed < duration)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(token);
            byte[] buffer = new byte[1024 * 128];

            while (stopwatch.Elapsed < duration)
            {
                int read = await stream.ReadAsync(buffer, token);
                if (read <= 0)
                {
                    break;
                }

                totalBytes += read;
            }
        }

        stopwatch.Stop();
        double seconds = Math.Max(0.001d, stopwatch.Elapsed.TotalSeconds);
        double downloadMbps = Math.Round(totalBytes * 8d / seconds / 1_000_000d, 2);
        int theoreticalRooms = Math.Max(0, (int)Math.Floor(downloadMbps / normalizedRoomMbps));
        int safeRooms = Math.Max(0, (int)Math.Floor(downloadMbps * 0.8d / normalizedRoomMbps));

        return new NetworkCapacityTestResult(
            downloadMbps,
            normalizedRoomMbps,
            theoreticalRooms,
            safeRooms,
            totalBytes,
            Math.Round(seconds, 2),
            url);
    }
}
