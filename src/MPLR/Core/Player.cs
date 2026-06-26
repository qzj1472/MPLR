using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MPLR.Core;

public sealed class Player
{
    private const string FfplayExeName = "ffplay.exe";
    private const string FfprobeExeName = "ffprobe.exe";
    private const int PreviewWindowWidth = 960;
    private const int PreviewWindowHeight = 540;
    private const int PreviewWindowCenteringSeconds = 8;
    private const int PreviewWindowCenteringIntervalMilliseconds = 120;

    public static Task PlayAsync(string mediaPath, bool isSeekable = false)
    {
        return PreviewAsync(mediaPath, Path.GetFileNameWithoutExtension(mediaPath), mediaPath);
    }

    public static async Task PreviewAsync(
        string roomUrl,
        string nickName,
        string recordUrl = "",
        string hlsUrl = "",
        string flvUrl = "",
        string headers = "",
        string title = "")
    {
        List<string> cachedPreviewUrls = SelectPreviewUrls(hlsUrl, flvUrl, recordUrl);
        List<string> previewUrls = [];
        string previewTitle = SelectPreviewTitle(title, nickName);
        string previewHeaders = headers;

        if (ShouldResolveFreshStream(roomUrl))
        {
            ISpiderResult? spiderResult = await Task.Run(() => Spider.GetResult(roomUrl));

            if (spiderResult != null)
            {
                previewUrls = SelectPreviewUrls(spiderResult.HlsUrl, spiderResult.FlvUrl, spiderResult.RecordUrl);
                previewTitle = SelectPreviewTitle(spiderResult.Title, spiderResult.Nickname, nickName);
                previewHeaders = SelectPreviewHeaders(headers, spiderResult.Headers);

                AppSessionLogger.Event("info", "player", "preview_stream_resolved", "preview stream resolved", new
                {
                    roomUrl,
                    isLiveStreaming = spiderResult.IsLiveStreaming,
                    hasRecordUrl = !string.IsNullOrWhiteSpace(spiderResult.RecordUrl),
                    hasHlsUrl = !string.IsNullOrWhiteSpace(spiderResult.HlsUrl),
                    hasFlvUrl = !string.IsNullOrWhiteSpace(spiderResult.FlvUrl),
                });
            }
        }

        if (previewUrls.Count == 0)
        {
            previewUrls = cachedPreviewUrls;
        }

        if (previewUrls.Count == 0)
        {
            AppSessionLogger.Event("warn", "player", "preview_no_stream", "preview has no playable stream", new
            {
                roomUrl,
                nickName,
            });
            _ = MessageBox.Warning("PlayerErrorOfNoPreviewStream".Tr());
            return;
        }

        string? ffplayPath = FindFfplay();

        if (string.IsNullOrWhiteSpace(ffplayPath))
        {
            AppSessionLogger.Event("error", "player", "preview_ffplay_not_found", "ffplay executable not found", new
            {
                roomUrl,
            });
            _ = MessageBox.Warning("PlayerErrorOfFFplayNotFound".Tr());
            return;
        }

        try
        {
            string preparedHeaders = PreparePreviewHeaders(previewHeaders, roomUrl, previewUrls);
            PreviewSource previewSource = await SelectPreviewSourceAsync(previewUrls, preparedHeaders);
            VideoSize? previewSize = CalculatePreviewVideoSize(previewSource.Size);
            ProcessStartInfo startInfo = CreateFfplayStartInfo(ffplayPath, previewSource.Url, previewTitle, preparedHeaders, previewSize);
            Process? process = Process.Start(startInfo);

            if (process != null)
            {
                AppSessionLogger.Event("info", "player", "preview_started", "ffplay preview process started", new
                {
                    roomUrl,
                    previewUrl = RedactUrl(previewSource.Url),
                    ffplayPath,
                    processId = process.Id,
                    width = previewSize?.Width,
                    height = previewSize?.Height,
                });
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _ = LogPreviewExitAsync(process, roomUrl, previewSource.Url);
                _ = ManagePreviewWindowAsync(process.Id);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            AppSessionLogger.WriteException(e);
            _ = MessageBox.Warning("PlayerErrorOfNoPlayerFound".Tr());
        }
    }

    private static ProcessStartInfo CreateFfplayStartInfo(string ffplayPath, string previewUrl, string title, string headers, VideoSize? previewSize)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = ffplayPath,
            WorkingDirectory = Path.GetDirectoryName(ffplayPath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-nostats");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("-window_title");
        startInfo.ArgumentList.Add(title);
        startInfo.ArgumentList.Add("-protocol_whitelist");
        startInfo.ArgumentList.Add("file,http,https,tcp,tls,crypto,data,udp,rtp,rtmp,rtmps");
        startInfo.ArgumentList.Add("-analyzeduration");
        startInfo.ArgumentList.Add("10000000");
        startInfo.ArgumentList.Add("-probesize");
        startInfo.ArgumentList.Add("10000000");
        if (previewSize.HasValue)
        {
            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add(previewSize.Value.Width.ToString());
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add(previewSize.Value.Height.ToString());
        }
        startInfo.ArgumentList.Add("-autoexit");
        startInfo.ArgumentList.Add("-fflags");
        startInfo.ArgumentList.Add("nobuffer");
        startInfo.ArgumentList.Add("-flags");
        startInfo.ArgumentList.Add("low_delay");

        if (!string.IsNullOrWhiteSpace(headers))
        {
            AddHeaderProtocolOptions(startInfo, headers);
            startInfo.ArgumentList.Add("-headers");
            startInfo.ArgumentList.Add(headers.EndsWith("\r\n", StringComparison.Ordinal) ? headers : headers + "\r\n");
        }

        startInfo.ArgumentList.Add(previewUrl);
        return startInfo;
    }

    private static async Task<PreviewSource> SelectPreviewSourceAsync(IReadOnlyList<string> previewUrls, string headers)
    {
        foreach (string previewUrl in previewUrls)
        {
            VideoSize? size = await ProbeVideoSizeAsync(previewUrl, headers);
            if (size != null)
            {
                AppSessionLogger.Event("info", "player", "preview_probe_succeeded", "preview probe succeeded", new
                {
                    previewUrl = RedactUrl(previewUrl),
                    width = size.Value.Width,
                    height = size.Value.Height,
                });
                return new PreviewSource(previewUrl, size.Value);
            }
        }

        AppSessionLogger.Event("warn", "player", "preview_probe_fallback", "preview probe failed for all streams", new
        {
            count = previewUrls.Count,
            firstPreviewUrl = RedactUrl(previewUrls[0]),
        });
        return new PreviewSource(previewUrls[0], null);
    }

    private static async Task<VideoSize?> ProbeVideoSizeAsync(string previewUrl, string headers)
    {
        string? ffprobePath = FindFfprobe();
        if (string.IsNullOrWhiteSpace(ffprobePath))
        {
            return null;
        }

        try
        {
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(8));
            ProcessStartInfo startInfo = new()
            {
                FileName = ffprobePath,
                WorkingDirectory = Path.GetDirectoryName(ffprobePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-protocol_whitelist");
            startInfo.ArgumentList.Add("file,http,https,tcp,tls,crypto,data,udp,rtp,rtmp,rtmps");
            startInfo.ArgumentList.Add("-allowed_extensions");
            startInfo.ArgumentList.Add("ALL");
            startInfo.ArgumentList.Add("-reconnect");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-reconnect_streamed");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-reconnect_at_eof");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-reconnect_on_network_error");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-reconnect_on_http_error");
            startInfo.ArgumentList.Add("4xx,5xx");
            startInfo.ArgumentList.Add("-rw_timeout");
            startInfo.ArgumentList.Add("15000000");
            startInfo.ArgumentList.Add("-timeout");
            startInfo.ArgumentList.Add("15000000");
            startInfo.ArgumentList.Add("-analyzeduration");
            startInfo.ArgumentList.Add("10000000");
            startInfo.ArgumentList.Add("-probesize");
            startInfo.ArgumentList.Add("10000000");
            startInfo.ArgumentList.Add("-live_start_index");
            startInfo.ArgumentList.Add("-3");
            startInfo.ArgumentList.Add("-multiple_requests");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-select_streams");
            startInfo.ArgumentList.Add("v:0");
            startInfo.ArgumentList.Add("-show_entries");
            startInfo.ArgumentList.Add("stream=width,height");
            startInfo.ArgumentList.Add("-of");
            startInfo.ArgumentList.Add("csv=p=0:s=x");

            if (!string.IsNullOrWhiteSpace(headers))
            {
                AddHeaderProtocolOptions(startInfo, headers);
                startInfo.ArgumentList.Add("-headers");
                startInfo.ArgumentList.Add(headers.EndsWith("\r\n", StringComparison.Ordinal) ? headers : headers + "\r\n");
            }

            startInfo.ArgumentList.Add(previewUrl);

            using Process process = new()
            {
                StartInfo = startInfo,
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
            string error = await process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);
            await process.WaitForExitAsync(cancellationTokenSource.Token);

            if (process.ExitCode != 0)
            {
                AppSessionLogger.Event("warn", "player", "preview_probe_failed", TrimError(error), new
                {
                    previewUrl = RedactUrl(previewUrl),
                    exitCode = process.ExitCode,
                    stdoutLength = output.Length,
                    stderrLength = error.Length,
                });
                return null;
            }

            foreach (string line in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = line.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 &&
                    int.TryParse(parts[0], out int width) &&
                    int.TryParse(parts[1], out int height) &&
                    width > 0 &&
                    height > 0)
                {
                    return new VideoSize(width, height);
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            AppSessionLogger.Event("warn", "player", "preview_probe_exception", e.Message, new
            {
                previewUrl = RedactUrl(previewUrl),
                type = e.GetType().FullName,
            });
        }

        return null;
    }

    private static VideoSize? CalculatePreviewVideoSize(VideoSize? sourceSize)
    {
        if (!sourceSize.HasValue)
        {
            return null;
        }

        System.Windows.Forms.Screen screen = GetPreviewScreen();
        System.Drawing.Rectangle workingArea = screen.WorkingArea;
        WindowFrameMetrics frameMetrics = GetWindowFrameMetrics();
        int maxVideoWidth = Math.Max(1, workingArea.Width - frameMetrics.Width);
        int maxVideoHeight = Math.Max(1, workingArea.Height - frameMetrics.Height);
        int inset = GetPreviewWindowInset();
        maxVideoWidth = Math.Max(1, maxVideoWidth - inset);
        maxVideoHeight = Math.Max(1, maxVideoHeight - inset);
        double scale = Math.Min((double)maxVideoWidth / sourceSize.Value.Width, (double)maxVideoHeight / sourceSize.Value.Height);

        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return CalculateFallbackPreviewVideoSize(maxVideoWidth, maxVideoHeight);
        }

        int width = Math.Max(1, (int)Math.Round(sourceSize.Value.Width * scale));
        int height = Math.Max(1, (int)Math.Round(sourceSize.Value.Height * scale));
        return new VideoSize(width, height);
    }

    private static VideoSize CalculateFallbackPreviewVideoSize(int maxVideoWidth, int maxVideoHeight)
    {
        VideoSize fallback = new(PreviewWindowWidth, PreviewWindowHeight);
        double scale = Math.Min((double)maxVideoWidth / fallback.Width, (double)maxVideoHeight / fallback.Height);

        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return fallback;
        }

        double targetScale = Math.Min(1d, scale * 0.85d);
        return new VideoSize(
            Math.Max(1, (int)Math.Round(fallback.Width * targetScale)),
            Math.Max(1, (int)Math.Round(fallback.Height * targetScale)));
    }

    private static int GetPreviewWindowInset()
    {
        return Math.Max(24, GetSystemMetrics(SystemMetric.SM_CYCAPTION));
    }

    private static System.Windows.Forms.Screen GetPreviewScreen()
    {
        try
        {
            System.Windows.Window? window = System.Windows.Application.Current?.MainWindow;
            if (window != null)
            {
                IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    return System.Windows.Forms.Screen.FromHandle(handle);
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        return System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens.First();
    }

    private static WindowFrameMetrics GetWindowFrameMetrics()
    {
        int paddedBorder = Math.Max(0, GetSystemMetrics(SystemMetric.SM_CXPADDEDBORDER));
        int horizontalFrame = Math.Max(0, GetSystemMetrics(SystemMetric.SM_CXSIZEFRAME)) + paddedBorder;
        int verticalFrame = Math.Max(0, GetSystemMetrics(SystemMetric.SM_CYSIZEFRAME)) + paddedBorder;
        int caption = Math.Max(0, GetSystemMetrics(SystemMetric.SM_CYCAPTION));
        int width = horizontalFrame * 2;
        int height = caption + verticalFrame * 2;
        return new WindowFrameMetrics(width, height);
    }

    private static string? FindFfplay()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, FfplayExeName),
            Path.Combine(baseDirectory, "ffmpeg", FfplayExeName),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return FindExecutableOnPath(FfplayExeName);
    }

    private static string? FindFfprobe()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, FfprobeExeName),
            Path.Combine(baseDirectory, "ffmpeg", FfprobeExeName),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return FindExecutableOnPath(FfprobeExeName);
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string SelectPreviewUrl(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static List<string> SelectPreviewUrls(params string?[] values)
    {
        List<string> urls = [];

        foreach (string? value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string trimmed = value.Trim();
            if (!urls.Any(url => url.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                urls.Add(trimmed);
            }
        }

        return urls;
    }

    private static bool ShouldResolveFreshStream(string roomUrl)
    {
        return Uri.TryCreate(roomUrl, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task LogPreviewExitAsync(Process process, string roomUrl, string previewUrl)
    {
        try
        {
            await process.WaitForExitAsync();
            AppSessionLogger.Event(process.ExitCode == 0 ? "info" : "warn", "player", "preview_exited", "ffplay preview process exited", new
            {
                roomUrl,
                previewUrl = RedactUrl(previewUrl),
                processId = process.Id,
                exitCode = process.ExitCode,
            });
            process.Dispose();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            AppSessionLogger.Event("warn", "player", "preview_exit_trace_failed", e.Message, new
            {
                roomUrl,
                previewUrl = RedactUrl(previewUrl),
                type = e.GetType().FullName,
            });
        }
    }

    private static string TrimError(string value)
    {
        string[] lines = value
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(6)
            .ToArray();

        return lines.Length == 0 ? value.Trim() : string.Join(Environment.NewLine, lines);
    }

    private static string RedactUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Query))
        {
            return value;
        }

        UriBuilder builder = new(uri)
        {
            Query = string.Empty,
        };
        return builder.Uri.ToString();
    }

    private static string SelectPreviewTitle(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return $"{AppConfig.DisplayName} Preview";
    }

    private static string SelectPreviewHeaders(params string?[] values)
    {
        string result = string.Empty;

        foreach (string? value in values)
        {
            string normalized = NormalizeHeaders(value ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            foreach (string header in SplitHeaderBlock(normalized))
            {
                int separator = header.IndexOf(':');
                if (separator <= 0)
                {
                    result = AppendHeader(result, header);
                    continue;
                }

                string name = header[..separator].Trim();
                if (string.IsNullOrWhiteSpace(ExtractHeaderValue(result, name)))
                {
                    result = AppendHeader(result, header);
                }
            }
        }

        return result;
    }

    private static string PreparePreviewHeaders(string headers, string roomUrl, IReadOnlyList<string> previewUrls)
    {
        string userAgent = Configurations.UserAgent.Get();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = DefaultUserAgent();
        }

        string headerBlock = NormalizeHeaders(headers);
        headerBlock = EnsureHeader(headerBlock, "User-Agent", userAgent);

        string referer = SelectReferer(roomUrl, previewUrls);
        headerBlock = EnsureHeader(headerBlock, "Referer", referer);

        string origin = SelectOrigin(headerBlock, referer, roomUrl, previewUrls);
        headerBlock = EnsureHeader(headerBlock, "Origin", origin);

        return headerBlock;
    }

    private static string SelectReferer(string roomUrl, IReadOnlyList<string> previewUrls)
    {
        string platformReferer = SelectPlatformReferer(roomUrl) ??
            previewUrls.Select(SelectPlatformReferer).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ??
            string.Empty;

        if (!string.IsNullOrWhiteSpace(platformReferer))
        {
            return platformReferer;
        }

        if (Uri.TryCreate(roomUrl, UriKind.Absolute, out Uri? roomUri))
        {
            return $"{roomUri.Scheme}://{roomUri.Host}/";
        }

        foreach (string previewUrl in previewUrls)
        {
            if (Uri.TryCreate(previewUrl, UriKind.Absolute, out Uri? previewUri) &&
                (previewUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 previewUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return $"{previewUri.Scheme}://{previewUri.Host}/";
            }
        }

        return string.Empty;
    }

    private static string? SelectPlatformReferer(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.Contains("twitch.tv", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("ttvnw.net", StringComparison.OrdinalIgnoreCase))
        {
            return "https://www.twitch.tv/";
        }

        if (url.Contains("bilibili.com", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("bilivideo.com", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("bilivideo.cn", StringComparison.OrdinalIgnoreCase))
        {
            return "https://live.bilibili.com/";
        }

        return null;
    }

    private static string SelectOrigin(string headers, string referer, string roomUrl, IReadOnlyList<string> previewUrls)
    {
        string origin = ExtractHeaderValue(headers, "Origin");
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return origin;
        }

        foreach (string value in new[] { referer, SelectReferer(roomUrl, previewUrls) })
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            {
                return $"{uri.Scheme}://{uri.Host}";
            }
        }

        return string.Empty;
    }

    private static string NormalizeHeaders(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return string.Empty;
        }

        string[] parts = SplitHeaderBlock(headers);
        return string.Join("\r\n", parts.Select(NormalizeHeader).Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string[] SplitHeaderBlock(string headers)
    {
        List<string> parts = [];
        StringBuilder current = new();

        for (int i = 0; i < headers.Length; i++)
        {
            char ch = headers[i];
            if (ch == '\r' || ch == '\n' || IsHeaderSeparator(headers, i))
            {
                AddHeaderPart(parts, current);

                if (ch == '\r' && i + 1 < headers.Length && headers[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            current.Append(ch);
        }

        AddHeaderPart(parts, current);
        return [.. parts];
    }

    private static bool IsHeaderSeparator(string value, int index)
    {
        if (value[index] != ';')
        {
            return false;
        }

        int start = index + 1;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        int colon = value.IndexOf(':', start);
        if (colon <= start)
        {
            return false;
        }

        int nextBreak = value.IndexOfAny([';', '\r', '\n'], start);
        if (nextBreak >= 0 && nextBreak < colon)
        {
            return false;
        }

        return value[start..colon].All(IsHeaderNameChar);
    }

    private static bool IsHeaderNameChar(char ch)
    {
        return char.IsAsciiLetterOrDigit(ch) || ch == '-';
    }

    private static void AddHeaderPart(List<string> parts, StringBuilder current)
    {
        string part = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(part))
        {
            parts.Add(part);
        }

        current.Clear();
    }

    private static string NormalizeHeader(string header)
    {
        int separator = header.IndexOf(':');
        if (separator <= 0)
        {
            return header.Trim();
        }

        string key = header[..separator].Trim();
        string value = header[(separator + 1)..].Trim();
        return $"{key}: {value}";
    }

    private static string ExtractHeaderValue(string headers, string name)
    {
        foreach (string header in headers.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = header.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            if (header[..separator].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return header[(separator + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    private static void AddHeaderProtocolOptions(ProcessStartInfo startInfo, string headers)
    {
        string userAgent = ExtractHeaderValue(headers, "User-Agent");
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            startInfo.ArgumentList.Add("-user_agent");
            startInfo.ArgumentList.Add(userAgent);
        }

        string referer = ExtractHeaderValue(headers, "Referer");
        if (!string.IsNullOrWhiteSpace(referer))
        {
            startInfo.ArgumentList.Add("-referer");
            startInfo.ArgumentList.Add(referer);
        }
    }

    private static string EnsureHeader(string headers, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(ExtractHeaderValue(headers, name)))
        {
            return headers;
        }

        return AppendHeader(headers, $"{name}: {value}");
    }

    private static string AppendHeader(string headers, string header)
    {
        return string.IsNullOrWhiteSpace(headers)
            ? header
            : headers.TrimEnd() + "\r\n" + header;
    }

    private static string DefaultUserAgent()
    {
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    }

    private static async Task ManagePreviewWindowAsync(int processId)
    {
        DateTime deadline = DateTime.Now.AddSeconds(PreviewWindowCenteringSeconds);
        bool grouped = false;

        while (DateTime.Now < deadline)
        {
            await Task.Delay(PreviewWindowCenteringIntervalMilliseconds);

            try
            {
                using Process process = Process.GetProcessById(processId);
                process.Refresh();

                if (process.HasExited)
                {
                    return;
                }

                IntPtr handle = SelectPreviewWindowHandle(process);
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                if (!grouped)
                {
                    grouped = TryGroupPreviewWindows(process);
                }

                _ = FitWindowIntoWorkingArea(handle);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return;
            }
        }
    }

    private static IntPtr SelectPreviewWindowHandle(Process process)
    {
        IntPtr[] handles = Interop.GetWindowHandleByProcessId(process.Id);
        return handles.FirstOrDefault(handle => handle != IntPtr.Zero);
    }

    private static bool TryGroupPreviewWindows(Process process)
    {
        bool grouped = false;
        foreach (IntPtr handle in Interop.GetWindowHandleByProcessId(process.Id))
        {
            grouped |= TaskbarGrouping.SetPreviewWindowAssociation(handle, process);
        }

        return grouped;
    }

    private static bool FitWindowIntoWorkingArea(IntPtr handle)
    {
        if (!GetWindowRect(handle, out NativeRect rect))
        {
            return false;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        NativeRect workingArea = GetWindowWorkingArea(handle);
        int workingWidth = workingArea.Right - workingArea.Left;
        int workingHeight = workingArea.Bottom - workingArea.Top;
        int inset = GetPreviewWindowInset();
        int maxWidth = Math.Max(1, workingWidth - inset);
        int maxHeight = Math.Max(1, workingHeight - inset);

        if (width > maxWidth || height > maxHeight)
        {
            double scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
            width = Math.Max(1, (int)Math.Floor(width * scale));
            height = Math.Max(1, (int)Math.Floor(height * scale));
        }

        int left = workingArea.Left + (workingWidth - width) / 2;
        int top = workingArea.Top + (workingHeight - height) / 2;

        left = Math.Clamp(left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
        top = Math.Clamp(top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - height));

        _ = SetWindowPos(handle, IntPtr.Zero, left, top, width, height, SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);
        return true;
    }

    private static NativeRect GetWindowWorkingArea(IntPtr handle)
    {
        IntPtr monitor = MonitorFromWindow(handle, MonitorDefaultToFlags.Nearest);
        MonitorInfo monitorInfo = new()
        {
            Size = Marshal.SizeOf<MonitorInfo>(),
        };

        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo)
            ? monitorInfo.Work
            : RectFromDrawingRectangle(System.Windows.Forms.Screen.FromHandle(handle).WorkingArea);
    }

    private static NativeRect RectFromDrawingRectangle(System.Drawing.Rectangle rectangle)
    {
        return new NativeRect()
        {
            Left = rectangle.Left,
            Top = rectangle.Top,
            Right = rectangle.Right,
            Bottom = rectangle.Bottom,
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlags flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(SystemMetric nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorDefaultToFlags dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private readonly record struct VideoSize(int Width, int Height);

    private readonly record struct PreviewSource(string Url, VideoSize? Size);

    private readonly record struct WindowFrameMetrics(int Width, int Height);

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoSize = 0x0001,
        NoZOrder = 0x0004,
        NoActivate = 0x0010,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;

        public NativeRect Monitor;

        public NativeRect Work;

        public uint Flags;
    }

    private enum MonitorDefaultToFlags : uint
    {
        Nearest = 2,
    }

    private enum SystemMetric
    {
        SM_CXSIZEFRAME = 32,
        SM_CYSIZEFRAME = 33,
        SM_CYCAPTION = 4,
        SM_CXPADDEDBORDER = 92,
    }
}

