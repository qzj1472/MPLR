using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TiktokLiveRec.Core;

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
        List<string> previewUrls = SelectPreviewUrls(hlsUrl, flvUrl, recordUrl);
        string previewTitle = SelectPreviewTitle(title, nickName);
        string previewHeaders = headers;

        if (previewUrls.Count == 0 && !string.IsNullOrWhiteSpace(roomUrl))
        {
            ISpiderResult? spiderResult = await Task.Run(() => Spider.GetResult(roomUrl));

            if (spiderResult != null)
            {
                previewUrls = SelectPreviewUrls(spiderResult.HlsUrl, spiderResult.FlvUrl, spiderResult.RecordUrl);
                previewTitle = SelectPreviewTitle(spiderResult.Title, spiderResult.Nickname, nickName);
                previewHeaders = spiderResult.Headers ?? string.Empty;
            }
        }

        if (previewUrls.Count == 0)
        {
            _ = MessageBox.Warning("PlayerErrorOfNoPreviewStream".Tr());
            return;
        }

        string? ffplayPath = FindFfplay();

        if (string.IsNullOrWhiteSpace(ffplayPath))
        {
            _ = MessageBox.Warning("PlayerErrorOfFFplayNotFound".Tr());
            return;
        }

        try
        {
            string preparedHeaders = PreparePreviewHeaders(previewHeaders, roomUrl);
            PreviewSource previewSource = await SelectPreviewSourceAsync(previewUrls, preparedHeaders);
            VideoSize previewSize = CalculatePreviewVideoSize(previewSource.Size);
            ProcessStartInfo startInfo = CreateFfplayStartInfo(ffplayPath, previewSource.Url, previewTitle, preparedHeaders, previewSize);
            Process? process = Process.Start(startInfo);

            if (process != null)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _ = CenterPreviewWindowAsync(process.Id);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            _ = MessageBox.Warning("PlayerErrorOfNoPlayerFound".Tr());
        }
    }

    private static ProcessStartInfo CreateFfplayStartInfo(string ffplayPath, string previewUrl, string title, string headers, VideoSize previewSize)
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
        startInfo.ArgumentList.Add("-allowed_extensions");
        startInfo.ArgumentList.Add("ALL");
        startInfo.ArgumentList.Add("-reconnect");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-reconnect_streamed");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-reconnect_at_eof");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-reconnect_delay_max");
        startInfo.ArgumentList.Add("5");
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
        startInfo.ArgumentList.Add("-x");
        startInfo.ArgumentList.Add(previewSize.Width.ToString());
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(previewSize.Height.ToString());
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
                return new PreviewSource(previewUrl, size.Value);
            }
        }

        return new PreviewSource(previewUrls[0], new VideoSize(PreviewWindowWidth, PreviewWindowHeight));
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
            _ = await process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);
            await process.WaitForExitAsync(cancellationTokenSource.Token);

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
        }

        return null;
    }

    private static VideoSize CalculatePreviewVideoSize(VideoSize sourceSize)
    {
        System.Windows.Forms.Screen screen = GetPreviewScreen();
        System.Drawing.Rectangle workingArea = screen.WorkingArea;
        WindowFrameMetrics frameMetrics = GetWindowFrameMetrics();
        int maxVideoWidth = Math.Max(1, workingArea.Width - frameMetrics.Width);
        int maxVideoHeight = Math.Max(1, workingArea.Height - frameMetrics.Height);
        double scale = Math.Min((double)maxVideoWidth / sourceSize.Width, (double)maxVideoHeight / sourceSize.Height);

        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return new VideoSize(PreviewWindowWidth, PreviewWindowHeight);
        }

        int width = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
        int height = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
        return new VideoSize(width, height);
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

    private static string PreparePreviewHeaders(string headers, string roomUrl)
    {
        string userAgent = Configurations.UserAgent.Get();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = DefaultUserAgent();
        }

        string headerBlock = NormalizeHeaders(headers);
        headerBlock = EnsureHeader(headerBlock, "User-Agent", userAgent);

        if (string.IsNullOrWhiteSpace(ExtractHeaderValue(headerBlock, "referer")) &&
            Uri.TryCreate(roomUrl, UriKind.Absolute, out Uri? uri))
        {
            headerBlock = EnsureHeader(headerBlock, "Referer", $"{uri.Scheme}://{uri.Host}/");
        }

        return headerBlock;
    }

    private static string NormalizeHeaders(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return string.Empty;
        }

        string[] parts = headers.Split(["\r\n", "\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("\r\n", parts.Select(NormalizeHeader).Where(item => !string.IsNullOrWhiteSpace(item)));
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

        return string.IsNullOrWhiteSpace(headers)
            ? $"{name}: {value}"
            : headers.TrimEnd() + "\r\n" + $"{name}: {value}";
    }

    private static string DefaultUserAgent()
    {
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
    }

    private static async Task CenterPreviewWindowAsync(int processId)
    {
        DateTime deadline = DateTime.Now.AddSeconds(PreviewWindowCenteringSeconds);

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

                IntPtr handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                if (CenterWindow(handle))
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return;
            }
        }
    }

    private static bool CenterWindow(IntPtr handle)
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

        System.Drawing.Rectangle workingArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        int left = workingArea.Left + (workingArea.Width - width) / 2;
        int top = workingArea.Top + (workingArea.Height - height) / 2;

        _ = SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoSize | SetWindowPosFlags.NoActivate);
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SetWindowPosFlags flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(SystemMetric nIndex);

    private readonly record struct VideoSize(int Width, int Height);

    private readonly record struct PreviewSource(string Url, VideoSize Size);

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

    private enum SystemMetric
    {
        SM_CXSIZEFRAME = 32,
        SM_CYSIZEFRAME = 33,
        SM_CYCAPTION = 4,
        SM_CXPADDEDBORDER = 92,
    }
}
