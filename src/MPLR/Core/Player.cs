using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MPLR.Threading;
using MPLR.Views;

namespace MPLR.Core;

public sealed class Player
{
    private const string FfprobeExeName = "ffprobe.exe";
    private const int PreviewWindowWidth = 960;
    private const int PreviewWindowHeight = 540;
    private const double PreviewScreenRatio = 0.85d;
    private static readonly Lazy<string?> FfprobePath = new(FindFfprobeCore, LazyThreadSafetyMode.ExecutionAndPublication);

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
        string previewTitle = SelectPreviewTitle(title, nickName);

        try
        {
            PreviewPlaybackRequest request = new(
                cachedPreviewUrls,
                previewTitle,
                headers,
                roomUrl,
                nickName);

            await ShowEmbeddedPreviewAsync(request);
            AppSessionLogger.Event("info", "player", "preview_requested", "embedded preview window opened", new
            {
                roomUrl,
                cachedPreviewUrlCount = cachedPreviewUrls.Count,
            });
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            AppSessionLogger.WriteException(e);
            _ = MessageBox.Warning("PlayerErrorOfNoPlayerFound".Tr());
        }
    }

    private static async Task ShowEmbeddedPreviewAsync(PreviewPlaybackRequest request)
    {
        System.Windows.Application? application = System.Windows.Application.Current;
        if (application?.Dispatcher == null)
        {
            throw new InvalidOperationException("WPF application dispatcher is unavailable.");
        }

        await application.Dispatcher.InvokeAsync(() =>
        {
            EmbeddedPreviewWindow window = new(request)
            {
                Owner = application.MainWindow,
            };
            window.Show();
        });
    }

    private static async Task<PreviewSource> SelectPreviewSourceAsync(IReadOnlyList<string> previewUrls, string headers)
    {
        if (previewUrls.Count == 1)
        {
            return new PreviewSource(previewUrls[0], null);
        }

        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));
        Task<PreviewSource?>[] probeTasks = previewUrls
            .Select(url => TryCreatePreviewSourceAsync(url, headers, cancellationTokenSource.Token))
            .ToArray();

        while (probeTasks.Length > 0)
        {
            Task<PreviewSource?> completedTask = await Task.WhenAny(probeTasks);
            PreviewSource? source = await completedTask;

            if (source.HasValue)
            {
                cancellationTokenSource.Cancel();
                return source.Value;
            }

            probeTasks = probeTasks.Where(task => task != completedTask).ToArray();
        }

        AppSessionLogger.Event("warn", "player", "preview_probe_fallback", "preview probe failed for all streams", new
        {
            count = previewUrls.Count,
            firstPreviewUrl = RedactUrl(previewUrls[0]),
        });
        return new PreviewSource(previewUrls[0], null);
    }

    private static async Task<PreviewSource?> TryCreatePreviewSourceAsync(string previewUrl, string headers, CancellationToken token)
    {
        try
        {
            VideoSize? size = await ProbeVideoSizeAsync(previewUrl, headers, token);
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
        catch (OperationCanceledException)
        {
        }

        return null;
    }

    private static async Task<VideoSize?> ProbeVideoSizeAsync(string previewUrl, string headers, CancellationToken token)
    {
        string? ffprobePath = FindFfprobe();
        if (string.IsNullOrWhiteSpace(ffprobePath))
        {
            return null;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = ffprobePath,
                WorkingDirectory = Path.GetDirectoryName(ffprobePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            token.ThrowIfCancellationRequested();

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
            startInfo.ArgumentList.Add("3000000");
            startInfo.ArgumentList.Add("-timeout");
            startInfo.ArgumentList.Add("3000000");
            startInfo.ArgumentList.Add("-analyzeduration");
            startInfo.ArgumentList.Add("1000000");
            startInfo.ArgumentList.Add("-probesize");
            startInfo.ArgumentList.Add("1000000");
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

            try
            {
                process.Start();
                ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);
                RuntimeResourceLogger.Register(process, "ffprobe", "preview_probe", previewUrl, null, new
                {
                    previewUrl = RedactUrl(previewUrl),
                });
                string output = await process.StandardOutput.ReadToEndAsync(token);
                string error = await process.StandardError.ReadToEndAsync(token);
                await process.WaitForExitAsync(token);

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

                foreach (string line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }

                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
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
        int maxVideoWidth = Math.Max(1, (int)Math.Floor(workingArea.Width * PreviewScreenRatio) - frameMetrics.Width);
        int maxVideoHeight = Math.Max(1, (int)Math.Floor(workingArea.Height * PreviewScreenRatio) - frameMetrics.Height);
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

    private static string? FindFfprobe()
    {
        return FfprobePath.Value;
    }

    private static string? FindFfprobeCore()
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

    private static string TrimError(string value)
    {
        string[] lines = value
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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
        foreach (string header in headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(SystemMetric nIndex);

    private readonly record struct VideoSize(int Width, int Height);

    private readonly record struct PreviewSource(string Url, VideoSize? Size);

    private readonly record struct WindowFrameMetrics(int Width, int Height);

    private enum SystemMetric
    {
        SM_CXSIZEFRAME = 32,
        SM_CYSIZEFRAME = 33,
        SM_CYCAPTION = 4,
        SM_CXPADDEDBORDER = 92,
    }
}

