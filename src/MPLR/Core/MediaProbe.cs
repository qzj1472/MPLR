using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MPLR.Extensions;
using MPLR.Threading;

namespace MPLR.Core;

internal sealed record MediaProbeResult(string Resolution, string Bitrate)
{
    public static MediaProbeResult Empty { get; } = new(string.Empty, string.Empty);
}

internal static class MediaProbe
{
    public static async Task<MediaProbeResult> ProbeAsync(string url, string headers = "")
    {
        string? ffprobePath = SearchFileHelper.SearchFiles(".", "ffprobe[\\.exe]").FirstOrDefault()
            ?? SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault()?.Replace("ffmpeg", "ffprobe", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(ffprobePath) || !File.Exists(ffprobePath))
        {
            return MediaProbeResult.Empty;
        }

        try
        {
            ProcessStartInfo startInfo = CreateProbeStartInfo(ffprobePath, url, headers);

            using Process process = new() { StartInfo = startInfo };
            process.Start();
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);
            RuntimeResourceLogger.Register(process, "ffprobe", "media_probe", url, null, new
            {
                url,
            });

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            if (!await Task.Run(() => process.WaitForExit(12000)))
            {
                KillProcessTree(process);
                return MediaProbeResult.Empty;
            }

            _ = await errorTask;
            string output = await outputTask;

            if (string.IsNullOrWhiteSpace(output))
            {
                return MediaProbeResult.Empty;
            }

            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            JsonElement stream = default;

            if (root.TryGetProperty("streams", out JsonElement streams) &&
                streams.ValueKind == JsonValueKind.Array &&
                streams.GetArrayLength() > 0)
            {
                stream = streams[0];
            }

            string resolution = TryGetInt(stream, "width", out int width) && TryGetInt(stream, "height", out int height) && IsUsableResolution(width, height)
                ? $"{width}x{height}"
                : string.Empty;

            long bitRate = TryGetLong(stream, "bit_rate");
            if (bitRate <= 0 && root.TryGetProperty("format", out JsonElement format))
            {
                bitRate = TryGetLong(format, "bit_rate");

                if (bitRate <= 0 && File.Exists(url))
                {
                    double duration = TryGetDouble(format, "duration");
                    long size = TryGetLong(format, "size");
                    if (duration > 0 && size > 0)
                    {
                        bitRate = (long)(size * 8d / duration);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(resolution))
            {
                resolution = await ProbeFrameResolutionAsync(url, headers ?? string.Empty);
            }

            return new MediaProbeResult(resolution, FormatBitrate(bitRate));
        }
        catch
        {
            return MediaProbeResult.Empty;
        }
    }

    private static ProcessStartInfo CreateProbeStartInfo(string ffprobePath, string url, string headers)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = ffprobePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-select_streams");
        startInfo.ArgumentList.Add("v:0");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("stream=width,height,bit_rate:format=bit_rate,duration,size");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        AddHeaders(startInfo, headers);
        startInfo.ArgumentList.Add(url);

        return startInfo;
    }

    private static bool TryGetInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out JsonElement json) &&
            (json.TryGetInt32(out value) || int.TryParse(json.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value));
    }

    private static long TryGetLong(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out JsonElement json))
        {
            return 0;
        }

        if (json.TryGetInt64(out long value))
        {
            return value;
        }

        return long.TryParse(json.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static double TryGetDouble(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out JsonElement json))
        {
            return 0;
        }

        if (json.TryGetDouble(out double value))
        {
            return value;
        }

        return double.TryParse(json.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static async Task<string> ProbeFrameResolutionAsync(string url, string headers)
    {
        string? ffmpegPath = SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault();

        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            return string.Empty;
        }

        string imagePath = Path.Combine(Path.GetTempPath(), $"tlr_probe_{Guid.NewGuid():N}.jpg");

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            AddHeaders(startInfo, headers);
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(url);
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-update");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add(imagePath);

            using Process process = new() { StartInfo = startInfo };
            process.Start();
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);
            RuntimeResourceLogger.Register(process, "ffmpeg", "frame_probe", url, null, new
            {
                url,
            });

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            if (!await Task.Run(() => process.WaitForExit(15000)))
            {
                KillProcessTree(process);
                return string.Empty;
            }

            _ = await outputTask;
            _ = await errorTask;

            if (!File.Exists(imagePath))
            {
                return string.Empty;
            }

            using System.Drawing.Image image = System.Drawing.Image.FromFile(imagePath);
            return IsUsableResolution(image.Width, image.Height) ? $"{image.Width}x{image.Height}" : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            try
            {
                if (File.Exists(imagePath))
                {
                    File.Delete(imagePath);
                }
            }
            catch
            {
            }
        }
    }

    private static void AddHeaders(ProcessStartInfo startInfo, string headers)
    {
        string trimmedHeaders = (headers ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedHeaders))
        {
            return;
        }

        startInfo.ArgumentList.Add("-headers");
        startInfo.ArgumentList.Add(trimmedHeaders.EndsWith('\n') ? trimmedHeaders : trimmedHeaders + "\r\n");
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static bool IsUsableResolution(int width, int height)
    {
        return width >= 320 && height >= 180;
    }

    private static string FormatBitrate(long bitRate)
    {
        if (bitRate <= 0)
        {
            return string.Empty;
        }

        return bitRate >= 1_000_000
            ? $"{bitRate / 1_000_000d:0.##} Mbps"
            : $"{bitRate / 1_000d:0.##} kbps";
    }
}

