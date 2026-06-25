using CommunityToolkit.Mvvm.Messaging;
using Flucli;
using Flucli.Utils.Extensions;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MPLR.Extensions;
using MPLR.Models;

namespace MPLR.Core;

public sealed class Recorder
{
    public RecordStatus RecordStatus { get; internal set; } = RecordStatus.Initialized;

    public CancellationTokenSource? TokenSource { get; private set; } = null;

    public string? Url { get; set; } = null;

    public string? FileName { get; set; } = null;

    public string? Parameters { get; set; } = null;

    public DateTime StartTime { get; private set; } = DateTime.MinValue;

    public DateTime EndTime { get; private set; } = DateTime.MinValue;

    public bool IsToSegment { get; set; } = false;

    public Task Start(RecorderStartInfo startInfo, CancellationTokenSource? tokenSource = null)
    {
        if (RecordStatus == RecordStatus.Recording)
        {
            // Already recording
            return Task.CompletedTask;
        }

        RecordStatus = RecordStatus.Recording;

        // Start a recording task that does not use the default ThreadPool.
        return Task.Factory.StartNew(async () =>
        {
            try
            {
                string? recorderPath = SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault();

                if (recorderPath == null)
                {
                    // Recorder not found so you should reinstall the program.
                    RecordStatus = RecordStatus.NotRecording;
                    return;
                }

                string saveFolder = SaveFolderHelper.GetRecordFolder(Configurations.SaveFolder.Get(), startInfo, DateTime.Now);
                if (!Directory.Exists(saveFolder))
                {
                    Directory.CreateDirectory(saveFolder);
                }

                string userAgent = Configurations.UserAgent.Get();
                string httpProxy = Configurations.ProxyUrl.Get();
                bool isUseProxy = Configurations.IsUseProxy.Get() && !string.IsNullOrWhiteSpace(httpProxy);
                int segmentTime = Configurations.SegmentTime.Get();
                bool isToSegment = Configurations.IsToSegment.Get() && segmentTime > 0;

                IsToSegment = isToSegment;
                Url = SelectInputUrl(startInfo);

                if (string.IsNullOrWhiteSpace(Url))
                {
                    RecordStatus = RecordStatus.NotRecording;
                    return;
                }

                DateTime now = DateTime.Now;
                string outputExtension = IsHlsUrl(Url, startInfo) || isToSegment ? "ts" : "flv";
                string fileName = BuildRecordFileName(startInfo, now);
                string fileNamePattern = isToSegment
                    ? $"{fileName}_%03d.{outputExtension}"
                    : $"{fileName}.{outputExtension}";

                FileName = Path.Combine(saveFolder, fileNamePattern);
                WriteMetadata(saveFolder, fileName, outputExtension, startInfo, now);

                bool isOversea = IsOverseaUrl(startInfo.RoomUrl) || IsOverseaUrl(Url);
                string rwTimeout = isOversea ? "50000000" : "15000000";
                string analyzeduration = isOversea ? "40000000" : "20000000";
                string probesize = isOversea ? "20000000" : "10000000";
                string bufsize = isOversea ? "15000k" : "8000k";
                string maxMuxingQueueSize = isOversea ? "2048" : "1024";

                string headers = (startInfo.Headers ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(headers) && !headers.EndsWith('\n'))
                {
                    headers += "\r\n";
                }

                if (string.IsNullOrWhiteSpace(userAgent))
                {
                    userAgent = "Mozilla/5.0 (Linux; Android 11; SAMSUNG SM-G973U) AppleWebKit/537.36 ("
                              + "KHTML, like Gecko) SamsungBrowser/14.2 Chrome/87.0.4280.141 Mobile "
                              + "Safari/537.36";
                }

                Parameters = new List<string>() {
                    "-y",                             // Overwrite output files.
                    "-v", "verbose",                  // Set logging level to `verbose`.
                    "-rw_timeout", rwTimeout,         // Set maximum time to wait for (network) read/write operations to complete, in microseconds to `15 seconds`.
                    "-loglevel", "error",             // Set logging level to `error`.
                    "-hide_banner",                   // Suppress printing banner.
                    "-user_agent", userAgent,         // Override the User-Agent header. If not specified the protocol will use a string describing the libavformat build. ("Lavf/<version>")
                    "-protocol_whitelist", "rtmp,crypto,file,http,https,tcp,tls,udp,rtp,httpproxy",
                                                      // Set a ","-separated list of allowed protocols.
                                                      // "ALL" matches all protocols.
                                                      // Protocols prefixed by "-" are disabled.
                                                      // All protocols are allowed by default but protocols used by an another protocol (nested protocols) are restricted to a per protocol subset.
                    "-thread_queue_size", "1024",     // For input, this option sets the maximum number of queued packets when reading from the file or device.
                                                      // With low latency / high rate live streams, packets may be discarded if they are not read in a timely manner; setting this value can force ffmpeg to use a separate input thread and read packets as soon as they arrive.
                                                      // By default ffmpeg only does this if multiple inputs are specified.
                                                      // For output, this option specified the maximum number of packets that may be queued to each muxing thread.
                    "-analyzeduration", analyzeduration, // Specify how many microseconds are analyzed to probe the input.
                                                      // A higher value will enable detecting more accurate information, but will increase latency.
                                                      // It defaults to 5,000,000 microseconds = 5 seconds.
                                                      // Set to 20,000,000 microseconds = 20 seconds.
                    "-probesize", probesize,          // Set probing size in bytes, i.e. the size of the data to analyze to get stream information.
                                                      // A higher value will enable detecting more information in case it is dispersed into the stream, but will increase latency.
                                                      // Must be an integer not lesser than 32. It is 5000000 by default.
                    "-fflags", "+discardcorrupt",     // Set format flags. Some are implemented for a limited number of formats.
                                                      // Set to +discardcorrupt: Discard corrupted packets.
                }
                .AddIf(isUseProxy, "-http_proxy", httpProxy)
                .AddIf(!string.IsNullOrWhiteSpace(headers), "-headers", headers)
                .AddIf(true,
                    "-i", Url,                        // Input infile.
                    "-bufsize", bufsize,              // Specifies the decoder buffer size, which determines the variability of the output bitrate.
                    "-sn",                            // Disable subtitle.
                    "-dn",                            // Disable data.
                    "-reconnect_delay_max", "60",     // Set the maximum delay in seconds after which to give up reconnecting.
                    "-reconnect_streamed",            // If set then even streamed/non seekable streams will be reconnected on errors.
                    "-reconnect_at_eof",              // If set then eof is treated like an error and causes reconnection, this is useful for live / endless streams.
                    "-max_muxing_queue_size", maxMuxingQueueSize, // When transcoding audio and/or video streams, ffmpeg will not begin writing into the output until it has one packet for each such stream.
                                                      // While waiting for that to happen, packets for other streams are buffered.
                                                      // This option sets the size of this buffer, in packets, for the matching output stream.
                                                      // The default value of this option should be high enough for most uses, so only touch this option if you are sure that you need it.
                    "-correct_ts_overflow", "1",      // Correct single timestamp overflows if set to 1. Default is 1.
                    "-avoid_negative_ts", "1",
                    "-c:v", "copy",                   // Video codec name.
                    "-c:a", "copy",                   // Audio codec name.
                    "-map", "0"                       // Set input stream mapping.
                )
                .AddIf(isToSegment,
                    "-f", "segment",
                    "-segment_time", segmentTime.ToString(), // in secs
                    "-segment_format", "mpegts",
                    "-reset_timestamps", "1"
                )
                .AddIf(true, FileName) // _%03d
                .ToArguments();
                TokenSource = tokenSource ?? new CancellationTokenSource();

                EndTime = DateTime.MinValue;
                StartTime = DateTime.Now;

                CliResult result = await recorderPath
                    .WithArguments(Parameters)
                    .WithEnvironmentVariable(
                    [
                        (isUseProxy ? "http_proxy" : "__TIKTOKLIVEREC_IGNORE_HTTP_PROXY__", "http://" + httpProxy),
                        (isUseProxy ? "https_proxy" : "__TIKTOKLIVEREC_IGNORE_HTTPS_PROXY__", "http://" + httpProxy),
                    ])
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(OnStandardErrorReceived, Encoding.UTF8))
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(OnStandardOutputReceived, Encoding.UTF8))
                    .ExecuteAsync(cancellationToken: TokenSource.Token);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            try
            {
                // Converter to target format if recorded.
                if (File.Exists(FileName))
                {
                    string formatArrow = Configurations.RecordFormat.Get();

                    if (!string.IsNullOrEmpty(formatArrow) && formatArrow.Contains("->"))
                    {
                        formatArrow = "." + formatArrow.Split('>')[1].Trim().ToLower();

                        // Execute the converter asynchronously.
                        // So don't use await here.
                        _ = new Converter().ExecuteAsync(FileName, formatArrow)
                            .ContinueWith(task =>
                            {
                                // Remove only the conversion is successful.
                                if (task.Result && Configurations.IsRemoveTs.Get())
                                {
                                    try
                                    {
                                        File.Delete(FileName);
                                        FileName = Path.ChangeExtension(FileName, formatArrow);
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine(e);
                                    }
                                }
                            })
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            EndTime = DateTime.Now;
            RecordStatus = RecordStatus.NotRecording;
        }, TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        TokenSource?.Cancel();
    }

    private static string SelectInputUrl(RecorderStartInfo startInfo)
    {
        if (!string.IsNullOrWhiteSpace(startInfo.RecordUrl))
        {
            return startInfo.RecordUrl;
        }

        if (!string.IsNullOrWhiteSpace(startInfo.HlsUrl))
        {
            return startInfo.HlsUrl;
        }

        return startInfo.FlvUrl;
    }

    private static bool IsHlsUrl(string url, RecorderStartInfo startInfo)
    {
        return url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ||
               url == startInfo.HlsUrl;
    }

    private static string BuildRecordFileName(RecorderStartInfo startInfo, DateTime now)
    {
        string rule = Configurations.SaveFileNameRule.Get() switch
        {
            1 => "{主播名}_{录制时间}_{分辨率}",
            2 => "{平台}_{主播名}_{录制时间}",
            3 => "{平台}_{主播名}_{录制时间}_{分辨率}",
            4 => Configurations.SaveFileNameCustomRule.Get(),
            _ => "{主播名}_{录制时间}",
        };

        string nickName = string.IsNullOrWhiteSpace(startInfo.NickName) ? "Unknown" : startInfo.NickName;
        string fileName = rule
            .Replace("{主播名}", nickName)
            .Replace("{主播uid}", GetUidFromRoomUrl(startInfo.RoomUrl))
            .Replace("{平台}", startInfo.Platform)
            .Replace("{录制时间}", now.ToString("yyyy-MM-dd_HH-mm-ss"))
            .Replace("{分辨率}", startInfo.Resolution);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{nickName}_{now:yyyy-MM-dd_HH-mm-ss}";
        }

        fileName = fileName.SanitizeFileName().ReplaceTrailingDotsWithUnderscores();
        return string.IsNullOrWhiteSpace(fileName) ? $"{nickName.SanitizeFileName()}_{now:yyyy-MM-dd_HH-mm-ss}" : fileName;
    }

    private static void WriteMetadata(string saveFolder, string fileName, string outputExtension, RecorderStartInfo startInfo, DateTime now)
    {
        try
        {
            string metadataPath = Path.Combine(saveFolder, $"{fileName}.mplr.json");
            VideoRecordingMetadata metadata = new()
            {
                FileName = $"{fileName}.{outputExtension}",
                NickName = startInfo.NickName,
                RoomUrl = startInfo.RoomUrl,
                Platform = startInfo.Platform,
                Title = startInfo.Title,
                Resolution = startInfo.Resolution,
                Bitrate = startInfo.Bitrate,
                CoverPath = startInfo.CoverPath,
                RecordedAt = now,
            };

            File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private static string GetUidFromRoomUrl(string roomUrl)
    {
        if (string.IsNullOrWhiteSpace(roomUrl) || !Uri.TryCreate(roomUrl, UriKind.Absolute, out Uri? uri))
        {
            return string.Empty;
        }

        string uid = uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
        return string.IsNullOrWhiteSpace(uid) ? string.Empty : uid.SanitizeFileName();
    }

    private static bool IsOverseaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        string[] hosts =
        [
            "tiktok.com",
            "sooplive",
            "pandalive",
            "winktv",
            "flextv",
            "ttinglive",
            "popkontv",
            "twitch.tv",
            "liveme.com",
            "showroom-live.com",
            "chzzk.naver.com",
            "shopee",
            "shp.ee",
            "youtube.com",
            "youtu.be",
            "faceit.com",
            "picarto.tv",
        ];

        return hosts.Any(host => url.Contains(host, StringComparison.OrdinalIgnoreCase));
    }

    private Task OnStandardErrorReceived(string data, CancellationToken token)
    {
        // TODO
        Debug.WriteLine(data);
        _ = WeakReferenceMessenger.Default.Send(new RecorderMessage()
        {
            DataType = StandardData.StandardError,
            Data = data,
        });
        return Task.CompletedTask;
    }

    private Task OnStandardOutputReceived(string data, CancellationToken token)
    {
        // TODO
        Debug.WriteLine(data);
        _ = WeakReferenceMessenger.Default.Send(new RecorderMessage()
        {
            DataType = StandardData.StandardOutput,
            Data = data,
        });
        return Task.CompletedTask;
    }
}

public enum RecordStatus
{
    Initialized,
    Disabled,
    NotRecording,
    Recording,

    [Obsolete("Should retry recording instead of pushing an Error Status")]
    Error,
}

public record RecorderStartInfo
{
    public string NickName { get; set; } = string.Empty;

    public string RoomUrl { get; set; } = string.Empty;

    public string FlvUrl { get; set; } = string.Empty;

    public string HlsUrl { get; set; } = string.Empty;

    public string RecordUrl { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Resolution { get; set; } = string.Empty;

    public string Headers { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Bitrate { get; set; } = string.Empty;

    public string CoverPath { get; set; } = string.Empty;
}

public sealed class VideoRecordingMetadata
{
    public string FileName { get; set; } = string.Empty;

    public string NickName { get; set; } = string.Empty;

    public string RoomUrl { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Resolution { get; set; } = string.Empty;

    public string Bitrate { get; set; } = string.Empty;

    public string CoverPath { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; } = DateTime.MinValue;
}

file static class FileNameSanitizer
{
    /// <summary>
    /// Sanitizes the input file name by replacing invalid characters with underscores.
    /// </summary>
    /// <param name="fileName">The input file name.</param>
    /// <returns>The sanitized file name.</returns>
    public static string SanitizeFileName(this string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));

        // Get invalid file name characters.
        char[] invalidChars = Path.GetInvalidFileNameChars();

        // Replace each invalid character with an underscore.
        return string.Concat(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
    }

    /// <summary>
    /// Replaces all trailing periods (.) in a string with underscores (_).
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The modified string with trailing periods replaced by underscores.</returns>
    public static string ReplaceTrailingDotsWithUnderscores(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        int i = input.Length - 1;
        while (i >= 0 && input[i] == '.')
        {
            i--;
        }

        // Replace trailing dots with underscores
        return string.Concat(input.AsSpan(0, i + 1), new string('_', input.Length - i - 1));
    }
}

file static class NoLinqExtension
{
    public static List<string> AddIf(this List<string> self, bool condition, params string[] items)
    {
        if (condition)
        {
            foreach (string item in items)
            {
                self.Add(item);
            }
        }

        return self;
    }
}

