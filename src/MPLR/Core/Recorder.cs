using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MPLR.Extensions;
using MPLR.Models;
using MPLR.Threading;

namespace MPLR.Core;

public sealed class Recorder
{
    public RecordStatus RecordStatus { get; internal set; } = RecordStatus.Initialized;

    public CancellationTokenSource? TokenSource { get; private set; } = null;

    private readonly object stateLock = new();

    private readonly object processLock = new();

    private Process? currentProcess = null;

    private Task? recordingTask = null;

    private bool stopRequested;

    private readonly List<string> recordedFilePatterns = [];

    public bool IsBusy => recordingTask is { IsCompleted: false };

    public string? Url { get; set; } = null;

    public string? FileName { get; set; } = null;

    private string? MetadataPath { get; set; } = null;

    public string? Parameters { get; set; } = null;

    public DateTime StartTime { get; private set; } = DateTime.MinValue;

    public DateTime EndTime { get; private set; } = DateTime.MinValue;

    public bool IsToSegment { get; set; } = false;

    public Task Start(RecorderStartInfo startInfo, CancellationTokenSource? tokenSource = null)
    {
        lock (stateLock)
        {
            if (RecordStatus == RecordStatus.Recording || recordingTask is { IsCompleted: false })
            {
                return recordingTask ?? Task.CompletedTask;
            }

            stopRequested = false;
            RecordStatus = RecordStatus.Recording;
            TokenSource = tokenSource ?? new CancellationTokenSource();
            recordingTask = Task.Factory.StartNew(
                () => RunAsync(startInfo, TokenSource.Token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            ).Unwrap();
            return recordingTask;
        }
    }

    private async Task RunAsync(RecorderStartInfo startInfo, CancellationToken token)
    {
        try
        {
            string? recorderPath = SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault();

            if (recorderPath == null)
            {
                RecordStatus = RecordStatus.NotRecording;
                return;
            }

            string saveFolder = SaveFolderHelper.GetRecordFolder(startInfo.SaveFolder, startInfo, DateTime.Now, startInfo.SaveFolderPathLevel);
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            string userAgent = Configurations.UserAgent.Get();
            string httpProxy = Configurations.ProxyUrl.Get();
            bool isUseProxy = Configurations.IsUseProxy.Get() && !string.IsNullOrWhiteSpace(httpProxy);
            int segmentTime = startInfo.SegmentTime;
            bool isToSegment = startInfo.IsToSegment && segmentTime > 0;
            bool isToSegmentBySize = isToSegment && SegmentTimeUnitHelper.IsSizeUnit(startInfo.SegmentTimeUnit);
            string? targetFormat = GetTargetFormat(startInfo.RecordFormat);

            IsToSegment = isToSegment;
            Url = SelectInputUrl(startInfo);

            if (string.IsNullOrWhiteSpace(Url))
            {
                RecordStatus = RecordStatus.NotRecording;
                return;
            }

            bool isOversea = IsOverseaUrl(startInfo.RoomUrl) || IsOverseaUrl(Url);
            string rwTimeout = isOversea ? "50000000" : "30000000";
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

            EndTime = DateTime.MinValue;
            StartTime = DateTime.Now;
            recordedFilePatterns.Clear();

            int attempt = 0;
            while (!token.IsCancellationRequested && !stopRequested)
            {
                DateTime now = DateTime.Now;
                string baseFileName = BuildRecordFileName(startInfo, now);
                RecordingPlan plan = CreateRecordingPlan(startInfo, saveFolder, baseFileName, targetFormat, isToSegment, isToSegmentBySize);
                FileName = plan.FileName;
                MetadataPath = WriteMetadata(saveFolder, baseFileName, plan.OutputExtension, startInfo, now);
                recordedFilePatterns.Add(plan.FileName);

                List<string> arguments = BuildArguments(
                    plan,
                    isUseProxy,
                    httpProxy,
                    headers,
                    userAgent,
                    rwTimeout,
                    analyzeduration,
                    probesize,
                    bufsize,
                    maxMuxingQueueSize,
                    segmentTime);

                Parameters = FormatArguments(arguments);
                int exitCode = await ExecuteRecorderAsync(recorderPath, arguments, isUseProxy, httpProxy, startInfo, plan, token);
                if (token.IsCancellationRequested || stopRequested || exitCode == 0)
                {
                    break;
                }

                attempt++;
                TimeSpan delay = TimeSpan.FromSeconds(Math.Min(8, attempt switch
                {
                    1 => 1,
                    2 => 3,
                    _ => 8,
                }));
                Toast.Warning($"网络带宽紧张或连接不稳定，正在尝试重连：{startInfo.NickName}");
                AppSessionLogger.Event("warn", "recorder", "record_reconnect_scheduled", "record reconnect scheduled", new
                {
                    startInfo.RoomUrl,
                    startInfo.NickName,
                    exitCode,
                    attempt,
                    delaySeconds = delay.TotalSeconds,
                });
                await Task.Delay(delay, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally
        {
            EndTime = DateTime.Now;
            lock (stateLock)
            {
                if (RecordStatus == RecordStatus.Recording)
                {
                    RecordStatus = RecordStatus.NotRecording;
                }
            }
            AppSessionLogger.Event("info", "recorder", "record_finished", "recording task finished", new
            {
                startInfo.RoomUrl,
                startInfo.NickName,
                FileName,
                stopRequested,
                startedAt = StartTime,
                endedAt = EndTime,
                durationSeconds = StartTime == DateTime.MinValue ? 0 : Math.Max(0, (EndTime - StartTime).TotalSeconds),
            });
            await ConvertRecordedFileAsync(startInfo);
            CleanOrphanMetadata();
        }
    }

    public void Stop()
    {
        stopRequested = true;
        RequestCurrentProcessExit();
        lock (stateLock)
        {
            TokenSource?.Cancel();
            if (RecordStatus == RecordStatus.Recording)
            {
                EndTime = DateTime.Now;
                RecordStatus = RecordStatus.NotRecording;
            }
        }
    }

    public void EndNowIfRecording()
    {
        lock (stateLock)
        {
            if (EndTime == DateTime.MinValue)
            {
                EndTime = DateTime.Now;
            }

            if (RecordStatus == RecordStatus.Recording)
            {
                RecordStatus = RecordStatus.NotRecording;
            }
        }
    }

    private RecordingPlan CreateRecordingPlan(RecorderStartInfo startInfo, string saveFolder, string baseFileName, string? targetFormat, bool isToSegment, bool isToSegmentBySize)
    {
        bool shouldRecordIntermediateTs = IsHlsUrl(Url ?? string.Empty, startInfo) ||
            isToSegment ||
            IsOptimizedTargetFormat(targetFormat);
        string outputExtension = shouldRecordIntermediateTs ? "ts" : "flv";
        string fileNamePattern = isToSegment
            ? $"{baseFileName}_%03d.{outputExtension}"
            : $"{baseFileName}.{outputExtension}";

        return new RecordingPlan(
            Path.Combine(saveFolder, fileNamePattern),
            outputExtension,
            "mpegts",
            isToSegment,
            isToSegmentBySize,
            outputExtension.Equals("mkv", StringComparison.OrdinalIgnoreCase) ||
            outputExtension.Equals("mp4", StringComparison.OrdinalIgnoreCase) ||
            outputExtension.Equals("ts", StringComparison.OrdinalIgnoreCase));
    }

    private List<string> BuildArguments(
        RecordingPlan plan,
        bool isUseProxy,
        string httpProxy,
        string headers,
        string userAgent,
        string rwTimeout,
        string analyzeduration,
        string probesize,
        string bufsize,
        string maxMuxingQueueSize,
        int segmentTime)
    {
        List<string> arguments =
        [
            "-y",
            "-v", "verbose",
            "-rw_timeout", rwTimeout,
            "-loglevel", "error",
            "-hide_banner",
            "-user_agent", userAgent,
            "-protocol_whitelist", "rtmp,crypto,file,http,https,tcp,tls,udp,rtp,httpproxy",
            "-thread_queue_size", "1024",
            "-analyzeduration", analyzeduration,
            "-probesize", probesize,
            "-fflags", "+discardcorrupt",
        ];

        arguments
            .AddIf(isUseProxy, "-http_proxy", httpProxy)
            .AddIf(!string.IsNullOrWhiteSpace(headers), "-headers", headers)
            .AddIf(true,
                "-i", Url ?? string.Empty,
                "-bufsize", bufsize,
                "-sn",
                "-dn",
                "-reconnect_delay_max", "60",
                "-reconnect", "1",
                "-reconnect_streamed",
                "1",
                "-reconnect_at_eof",
                "1",
                "-reconnect_on_network_error",
                "1",
                "-reconnect_on_http_error",
                "4xx,5xx",
                "-max_muxing_queue_size", maxMuxingQueueSize,
                "-correct_ts_overflow", "1",
                "-avoid_negative_ts", "1"
            )
            .AddIf(plan.IsOptimizedAudioEnabled,
                "-filter_complex", "[0:a:0]volume=30dB,acompressor=threshold=-10dB:ratio=3,alimiter=limit=0.316227766:level=false[aopt]",
                "-map", "0:v?",
                "-map", "0:a:0?",
                "-map", "[aopt]",
                "-c:v", "copy",
                "-c:a:0", "copy",
                "-c:a:1", "aac",
                "-metadata:s:a:0", "title=原音频",
                "-metadata:s:a:0", "handler_name=原音频",
                "-metadata:s:a:1", "title=优化音频",
                "-metadata:s:a:1", "handler_name=优化音频"
            )
            .AddIf(!plan.IsOptimizedAudioEnabled,
                "-map", "0",
                "-c:v", "copy",
                "-c:a", "copy"
            )
            .AddIf(plan.IsToSegment && !plan.IsToSegmentBySize,
                "-f", "segment",
                "-segment_time", segmentTime.ToString(),
                "-segment_time_delta", "0.05",
                "-segment_atclocktime", "0",
                "-segment_format", plan.SegmentFormat
            )
            .AddIf(plan.IsToSegmentBySize,
                "-f", "segment",
                "-segment_size", segmentTime.ToString(),
                "-segment_format", plan.SegmentFormat
            )
            .AddIf(plan.IsToSegment,
                "-reset_timestamps", "1"
            )
            .AddIf(true, plan.FileName);

        return arguments;
    }

    private async Task<int> ExecuteRecorderAsync(string recorderPath, List<string> arguments, bool isUseProxy, string httpProxy, RecorderStartInfo recorderStartInfo, RecordingPlan plan, CancellationToken token)
    {
        ProcessStartInfo processStartInfo = new()
        {
            FileName = recorderPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
        };

        foreach (string argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        if (isUseProxy)
        {
            processStartInfo.Environment["http_proxy"] = "http://" + httpProxy;
            processStartInfo.Environment["https_proxy"] = "http://" + httpProxy;
        }

        using Process process = new() { StartInfo = processStartInfo };
        process.Start();
        ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);
        RuntimeResourceLogger.Register(process, "ffmpeg", "record", recorderStartInfo.RoomUrl, recorderStartInfo.NickName, new
        {
            recorderStartInfo.Platform,
            plan.FileName,
            inputUrl = Url ?? string.Empty,
        });

        lock (processLock)
        {
            currentProcess = process;
        }

        Task errorTask = ReadPipeAsync(process.StandardError, OnStandardErrorReceived, CancellationToken.None);
        Task outputTask = ReadPipeAsync(process.StandardOutput, OnStandardOutputReceived, CancellationToken.None);
        bool wasCanceled = false;

        try
        {
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            Task cancellationTask = WaitForCancellationAsync(token);
            Task completedTask = await Task.WhenAny(exitTask, cancellationTask);

            if (completedTask == cancellationTask && token.IsCancellationRequested)
            {
                wasCanceled = true;
                await StopProcessGracefullyAsync(process);
            }
            else
            {
                await exitTask;
            }
        }
        finally
        {
            lock (processLock)
            {
                if (ReferenceEquals(currentProcess, process))
                {
                    currentProcess = null;
                }
            }
        }

        await Task.WhenAll(errorTask, outputTask);

        if (wasCanceled)
        {
            throw new OperationCanceledException(token);
        }

        return process.ExitCode;
    }

    private static async Task WaitForCancellationAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task ReadPipeAsync(StreamReader reader, Func<string, CancellationToken, Task> handler, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(token);
                if (line == null)
                {
                    break;
                }

                await handler(line, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ConvertRecordedFileAsync(RecorderStartInfo startInfo)
    {
        try
        {
            CleanEmptySegmentFiles();
            string? targetFormat = GetTargetFormat(startInfo.RecordFormat);

            if (string.IsNullOrWhiteSpace(targetFormat))
            {
                return;
            }

            string[] sourceFiles = GetRecordedSourceFiles()
                .Where(IsConvertibleSourceFile)
                .ToArray();

            if (LowBatteryProtection.ShouldDeferTranscode())
            {
                PendingTranscodeQueue.Enqueue(sourceFiles, targetFormat, startInfo.IsRemoveTs);
                return;
            }

            foreach (string sourceFile in sourceFiles)
            {
                if (await new Converter().ExecuteAsync(sourceFile, targetFormat) && startInfo.IsRemoveTs)
                {
                    TryDelete(sourceFile);

                    if (string.Equals(FileName, sourceFile, StringComparison.OrdinalIgnoreCase))
                    {
                        FileName = Path.ChangeExtension(sourceFile, targetFormat);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private string[] GetRecordedSourceFiles()
    {
        string[] patterns = recordedFilePatterns.Count > 0
            ? recordedFilePatterns.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : string.IsNullOrWhiteSpace(FileName) ? [] : [FileName];

        return patterns.SelectMany(GetRecordedSourceFilesForPattern)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetRecordedSourceFilesForPattern(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return [];
        }

        if (!fileName.Contains("%03d", StringComparison.Ordinal))
        {
            return File.Exists(fileName) ? [fileName] : [];
        }

        string? directory = Path.GetDirectoryName(fileName);
        string pattern = Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) || string.IsNullOrWhiteSpace(pattern))
        {
            return [];
        }

        string regexPattern = "^" + Regex.Escape(pattern).Replace("%03d", @"\d{3}") + "$";
        Regex regex = new(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return Directory.EnumerateFiles(directory)
            .Where(file => regex.IsMatch(Path.GetFileName(file)))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void CleanEmptySegmentFiles()
    {
        foreach (string file in GetRecordedSourceFiles())
        {
            try
            {
                FileInfo info = new(file);
                if (info.Exists && info.Length == 0)
                {
                    info.Delete();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }

    private static bool IsConvertibleSourceFile(string file)
    {
        try
        {
            FileInfo info = new(file);
            if (!info.Exists || info.Length == 0)
            {
                return false;
            }

            return info.Extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
                   info.Extension.Equals(".flv", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetTargetFormat(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("->", StringComparison.Ordinal))
        {
            return null;
        }

        string target = value.Split("->", StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty;
        return string.IsNullOrWhiteSpace(target) ? null : "." + target.TrimStart('.').ToLowerInvariant();
    }

    private static bool IsOptimizedTargetFormat(string? targetFormat)
    {
        return targetFormat is ".mkv" or ".mp4";
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void RequestCurrentProcessExit()
    {
        Process? process;
        lock (processLock)
        {
            process = currentProcess;
        }

        if (process != null)
        {
            RequestProcessExit(process);
        }
    }

    private static async Task StopProcessGracefullyAsync(Process process)
    {
        RequestProcessExit(process);

        if (await WaitForExitAsync(process, TimeSpan.FromSeconds(15)))
        {
            return;
        }

        KillProcessTree(process);
        await process.WaitForExitAsync(CancellationToken.None);
    }

    private static void RequestProcessExit(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("q");
                process.StandardInput.Flush();
            }
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or ObjectDisposedException)
        {
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            Task timeoutTask = Task.Delay(timeout);
            return await Task.WhenAny(exitTask, timeoutTask) == exitTask;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
        }
    }

    private static string FormatArguments(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(FormatArgument));
    }

    private static string FormatArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
            ? "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : argument;
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
        string rule = Math.Clamp(startInfo.SaveFileNameRule, 0, 4) switch
        {
            1 => "{主播名}_{录制时间}_{分辨率}",
            2 => "{平台}_{主播名}_{录制时间}",
            3 => "{平台}_{主播名}_{录制时间}_{分辨率}",
            4 => startInfo.SaveFileNameCustomRule,
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

    private void CleanOrphanMetadata()
    {
        if (string.IsNullOrWhiteSpace(MetadataPath) || !File.Exists(MetadataPath))
        {
            return;
        }

        bool hasVideo = GetAssociatedVideoFiles().Any(file =>
        {
            try
            {
                FileInfo info = new(file);
                return info.Exists && info.Length > 0;
            }
            catch
            {
                return false;
            }
        });

        if (!hasVideo)
        {
            TryDelete(MetadataPath);
        }
    }

    private IEnumerable<string> GetAssociatedVideoFiles()
    {
        if (string.IsNullOrWhiteSpace(MetadataPath))
        {
            return [];
        }

        string? directory = Path.GetDirectoryName(MetadataPath);
        string metadataName = Path.GetFileNameWithoutExtension(MetadataPath);
        if (metadataName.EndsWith(".mplr", StringComparison.OrdinalIgnoreCase))
        {
            metadataName = Path.GetFileNameWithoutExtension(metadataName);
        }

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) || string.IsNullOrWhiteSpace(metadataName))
        {
            return [];
        }

        HashSet<string> videoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".mkv",
            ".flv",
            ".ts",
        };

        return Directory.EnumerateFiles(directory)
            .Where(file =>
            {
                string extension = Path.GetExtension(file);
                if (!videoExtensions.Contains(extension))
                {
                    return false;
                }

                string stem = Path.GetFileNameWithoutExtension(file);
                return stem.Equals(metadataName, StringComparison.OrdinalIgnoreCase) ||
                       stem.StartsWith(metadataName + "_", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string? WriteMetadata(string saveFolder, string fileName, string outputExtension, RecorderStartInfo startInfo, DateTime now)
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
            return metadataPath;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return null;
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

    public string RecordFormat { get; set; } = "TS/FLV";

    public bool IsRemoveTs { get; set; }

    public bool IsToSegment { get; set; }

    public int SegmentTime { get; set; } = 1800;

    public int SegmentTimeUnit { get; set; }

    public string SaveFolder { get; set; } = string.Empty;

    public int SaveFolderPathLevel { get; set; }

    public int SaveFileNameRule { get; set; }

    public string SaveFileNameCustomRule { get; set; } = "{主播名}_{录制时间}";
}

internal sealed record RecordingPlan(
    string FileName,
    string OutputExtension,
    string SegmentFormat,
    bool IsToSegment,
    bool IsToSegmentBySize,
    bool IsOptimizedAudioEnabled);

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

