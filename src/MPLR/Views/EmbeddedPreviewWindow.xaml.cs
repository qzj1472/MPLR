using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using MPLR.Core;
using VlcMedia = LibVLCSharp.Shared.Media;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using FormsPanel = System.Windows.Forms.Panel;

namespace MPLR.Views;

public partial class EmbeddedPreviewWindow : Wpf.Ui.Controls.FluentWindow
{
    private const int DefaultNetworkCachingMilliseconds = 700;
    private static readonly object InitializeLock = new();
    private static bool isInitialized;

    private readonly PreviewPlaybackRequest request;
    private readonly CancellationTokenSource playbackCancellation = new();
    private readonly List<PreviewSource> previewSources = [];
    private readonly DispatcherTimer videoLayoutRefreshTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(80),
    };
    private readonly FormsPanel videoCanvas = new BlackPanel()
    {
        BackColor = System.Drawing.Color.Black,
        Dock = System.Windows.Forms.DockStyle.Fill,
        Margin = System.Windows.Forms.Padding.Empty,
        Padding = System.Windows.Forms.Padding.Empty,
    };
    private readonly FormsPanel videoPanel = new BlackPanel()
    {
        BackColor = System.Drawing.Color.Black,
        Dock = System.Windows.Forms.DockStyle.None,
        Margin = System.Windows.Forms.Padding.Empty,
        Padding = System.Windows.Forms.Padding.Empty,
    };
    private LibVLC? libVlc;
    private VlcMediaPlayer? mediaPlayer;
    private VlcMedia? media;
    private string activeHeaders = string.Empty;
    private string activeTitle;
    private int activeSourceIndex = -1;
    private int activeSourceVideoWidth;
    private int activeSourceVideoHeight;
    private bool hasStartedPlaying;
    private bool hasAppliedVideoFitMode;
    private bool isDisposed;
    private bool isSwitchingSource;

    public EmbeddedPreviewWindow(PreviewPlaybackRequest request)
    {
        this.request = request;
        activeTitle = SelectPreviewTitle(request.Title, request.NickName);
        InitializeComponent();
        videoCanvas.Controls.Add(videoPanel);
        VideoFormsHost.Child = videoCanvas;
        ApplyTitle(activeTitle);
        Loaded += EmbeddedPreviewWindowLoaded;
        Closed += EmbeddedPreviewWindowClosed;
        SizeChanged += EmbeddedPreviewWindowSizeChanged;
        SourceInitialized += EmbeddedPreviewWindowSourceInitialized;
        videoCanvas.Resize += VideoCanvasResize;
        videoPanel.Resize += VideoPanelResize;
        videoLayoutRefreshTimer.Tick += VideoLayoutRefreshTimerTick;
    }

    private async void EmbeddedPreviewWindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= EmbeddedPreviewWindowLoaded;
        RefreshVideoLayout();
        await Dispatcher.InvokeAsync(RefreshVideoLayout, DispatcherPriority.ApplicationIdle);
        await PrepareAndStartAsync(playbackCancellation.Token);
    }

    private void EmbeddedPreviewWindowSourceInitialized(object? sender, EventArgs e)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            nint style = GetWindowLongPtr(handle, GetWindowLongIndex.GWL_STYLE);
            _ = SetWindowLongPtr(handle, GetWindowLongIndex.GWL_STYLE, style | (nint)(WindowStyles.WS_CLIPCHILDREN | WindowStyles.WS_CLIPSIBLINGS));
        }
    }

    private async Task PrepareAndStartAsync(CancellationToken token)
    {
        try
        {
            List<string> previewUrls = SelectPreviewUrls(request.Urls);
            string previewHeaders = request.Headers;
            string previewTitle = activeTitle;

            if (previewUrls.Count == 0 && ShouldResolveFreshStream(request.RoomUrl))
            {
                ShowStatus("正在解析直播间...");
                UpdatePlaybackStatus("解析中");
                ISpiderResult? spiderResult = await Task.Run(() => Spider.GetResult(request.RoomUrl), token);
                token.ThrowIfCancellationRequested();

                if (spiderResult != null)
                {
                    previewUrls = SelectPreviewUrls(spiderResult.HlsUrl, spiderResult.FlvUrl, spiderResult.RecordUrl);
                    previewTitle = SelectPreviewTitle(spiderResult.Title, spiderResult.Nickname, request.NickName);
                    previewHeaders = SelectPreviewHeaders(request.Headers, spiderResult.Headers);
                    ApplyTitle(previewTitle);

                    AppSessionLogger.Event("info", "player", "preview_stream_resolved", "preview stream resolved", new
                    {
                        request.RoomUrl,
                        isLiveStreaming = spiderResult.IsLiveStreaming,
                        hasRecordUrl = !string.IsNullOrWhiteSpace(spiderResult.RecordUrl),
                        hasHlsUrl = !string.IsNullOrWhiteSpace(spiderResult.HlsUrl),
                        hasFlvUrl = !string.IsNullOrWhiteSpace(spiderResult.FlvUrl),
                    });
                }
            }

            if (previewUrls.Count == 0)
            {
                AppSessionLogger.Event("warn", "player", "preview_no_stream", "preview has no playable stream", new
                {
                    request.RoomUrl,
                    request.NickName,
                });
                ShowStatus("未解析到可预览的直播间画面");
                UpdatePlaybackStatus("无可播放流");
                return;
            }

            activeHeaders = PreparePreviewHeaders(previewHeaders, request.RoomUrl, previewUrls);
            previewSources.Clear();
            previewSources.AddRange(previewUrls.Select(url => new PreviewSource(url, activeHeaders)));
            await PlaySourceAsync(0, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            AppSessionLogger.WriteException(ex);
            ShowStatus("预览播放器初始化失败");
            UpdatePlaybackStatus("初始化失败");
        }
    }

    private async Task PlaySourceAsync(int index, CancellationToken token)
    {
        if (index < 0 || index >= previewSources.Count)
        {
            return;
        }

        activeSourceIndex = index;
        hasStartedPlaying = false;
        ShowStatus(index == 0 ? "正在连接预览流..." : "正在切换备用流...");
        UpdatePlaybackStatus(index == 0 ? "连接中" : "切换中");

        PreviewSource source = previewSources[index];
        PlayerSession session = await Task.Run(() => CreatePlayerSession(source), token);
        if (token.IsCancellationRequested || isDisposed)
        {
            session.Dispose();
            token.ThrowIfCancellationRequested();
            return;
        }

        ApplyPlayerSession(session);
        bool started = await Task.Run(() => session.MediaPlayer.Play(session.Media), token);

        if (!started)
        {
            await TryPlayNextSourceAsync("预览播放启动失败");
        }
    }

    private static PlayerSession CreatePlayerSession(PreviewSource source)
    {
        InitializeLibVlc();
        LibVLC libVlc = new("--no-video-title-show", "--no-osd", "--network-caching=700", "--live-caching=700", "--file-caching=300");
        VlcMediaPlayer mediaPlayer = new(libVlc)
        {
            EnableHardwareDecoding = true,
        };
        VlcMedia media = CreateMedia(libVlc, source);
        return new PlayerSession(libVlc, mediaPlayer, media);
    }

    private void ApplyPlayerSession(PlayerSession session)
    {
        DisposeCurrentSession();
        libVlc = session.LibVlc;
        mediaPlayer = session.MediaPlayer;
        media = session.Media;
        hasAppliedVideoFitMode = false;
        activeSourceVideoWidth = 0;
        activeSourceVideoHeight = 0;
        AttachPlayerEvents(mediaPlayer);
        mediaPlayer.Volume = (int)Math.Round(VolumeSlider.Value);
        EnsureVideoPanelHandle();
        mediaPlayer.Hwnd = videoPanel.Handle;
        PlayPauseButton.IsEnabled = true;
        RefreshVideoLayout();
    }

    private static void InitializeLibVlc()
    {
        if (isInitialized)
        {
            return;
        }

        lock (InitializeLock)
        {
            if (isInitialized)
            {
                return;
            }

            string libVlcDirectory = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
            if (Directory.Exists(libVlcDirectory))
            {
                LibVLCSharp.Shared.Core.Initialize(libVlcDirectory);
            }
            else
            {
                LibVLCSharp.Shared.Core.Initialize();
            }

            isInitialized = true;
        }
    }

    private static VlcMedia CreateMedia(LibVLC libVlc, PreviewSource source)
    {
        VlcMedia media = new(libVlc, CreateMediaUri(source.Url));
        media.AddOption($":network-caching={DefaultNetworkCachingMilliseconds}");
        media.AddOption($":live-caching={DefaultNetworkCachingMilliseconds}");
        media.AddOption(":file-caching=300");
        media.AddOption(":input-repeat=0");
        AddHeaderOptions(media, source.Headers);
        return media;
    }

    private static Uri CreateMediaUri(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return new Uri(Path.GetFullPath(source));
    }

    private static void AddHeaderOptions(VlcMedia media, string headers)
    {
        string userAgent = ExtractHeaderValue(headers, "User-Agent");
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            media.AddOption($":http-user-agent={userAgent}");
        }

        string referer = ExtractHeaderValue(headers, "Referer");
        if (!string.IsNullOrWhiteSpace(referer))
        {
            media.AddOption($":http-referrer={referer}");
        }

        string cookie = ExtractHeaderValue(headers, "Cookie");
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            media.AddOption($":http-cookie={cookie}");
        }
    }

    private void PlayPauseClick(object sender, RoutedEventArgs e)
    {
        if (mediaPlayer == null)
        {
            return;
        }

        if (mediaPlayer.IsPlaying)
        {
            mediaPlayer.SetPause(true);
            ShowStatus("已暂停");
            UpdatePlaybackStatus("已暂停");
        }
        else
        {
            mediaPlayer.SetPause(false);
            HideStatus();
            UpdatePlaybackStatus("正在播放");
        }
    }

    private void StopClick(object sender, RoutedEventArgs e)
    {
        playbackCancellation.Cancel();
        mediaPlayer?.Stop();
        PlayPauseButton.IsEnabled = false;
        ShowStatus("播放已停止");
        UpdatePlaybackStatus("已停止");
    }

    private void VolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.Volume = (int)Math.Round(e.NewValue);
        }
    }

    private void MediaPlayerPlaying(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            hasStartedPlaying = true;
            HideStatus();
            UpdatePlaybackStatus("正在播放");
            PlayPauseButton.IsEnabled = true;
            TryUpdateSourceVideoSize();
            RefreshVideoLayout();
        });
    }

    private void MediaPlayerPaused(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            ShowStatus("已暂停");
            UpdatePlaybackStatus("已暂停");
        });
    }

    private void MediaPlayerStopped(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!isDisposed)
            {
                UpdatePlaybackStatus("已停止");
            }
        });
    }

    private void MediaPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (e.Cache >= 100)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            UpdatePlaybackStatus($"缓冲 {e.Cache:0}%");
            if (!hasStartedPlaying)
            {
                ShowStatus($"正在缓冲 {e.Cache:0}%");
            }
        });
    }

    private void MediaPlayerEncounteredError(object? sender, EventArgs e)
    {
        AppSessionLogger.Event("warn", "player", "embedded_preview_error", "embedded preview player encountered an error", new
        {
            request.RoomUrl,
            previewUrl = activeSourceIndex >= 0 && activeSourceIndex < previewSources.Count ? RedactUrl(previewSources[activeSourceIndex].Url) : string.Empty,
        });
        _ = Dispatcher.BeginInvoke(() => _ = TryPlayNextSourceAsync("预览播放异常"));
    }

    private void MediaPlayerEndReached(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(Close);
    }

    private async Task TryPlayNextSourceAsync(string finalMessage)
    {
        if (isDisposed || isSwitchingSource)
        {
            return;
        }

        int nextIndex = activeSourceIndex + 1;
        if (nextIndex >= previewSources.Count)
        {
            DisposeCurrentSession();
            ShowStatus(finalMessage);
            UpdatePlaybackStatus(finalMessage);
            PlayPauseButton.IsEnabled = false;
            return;
        }

        isSwitchingSource = true;
        try
        {
            await PlaySourceAsync(nextIndex, playbackCancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            AppSessionLogger.WriteException(ex);
            ShowStatus(finalMessage);
            UpdatePlaybackStatus(finalMessage);
        }
        finally
        {
            isSwitchingSource = false;
        }
    }

    private void AttachPlayerEvents(VlcMediaPlayer player)
    {
        player.Playing += MediaPlayerPlaying;
        player.Paused += MediaPlayerPaused;
        player.Stopped += MediaPlayerStopped;
        player.Buffering += MediaPlayerBuffering;
        player.EncounteredError += MediaPlayerEncounteredError;
        player.EndReached += MediaPlayerEndReached;
    }

    private void DetachPlayerEvents(VlcMediaPlayer player)
    {
        player.Playing -= MediaPlayerPlaying;
        player.Paused -= MediaPlayerPaused;
        player.Stopped -= MediaPlayerStopped;
        player.Buffering -= MediaPlayerBuffering;
        player.EncounteredError -= MediaPlayerEncounteredError;
        player.EndReached -= MediaPlayerEndReached;
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusOverlay.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusOverlay.Visibility = Visibility.Collapsed;
    }

    private void UpdatePlaybackStatus(string message)
    {
        PlaybackStateText.Text = message;
    }

    private void ApplyTitle(string title)
    {
        activeTitle = SelectPreviewTitle(title, request.NickName);
        Title = activeTitle;
        PreviewTitleBar.Title = activeTitle;
        PlayerTitleText.Text = activeTitle;
    }

    private void EmbeddedPreviewWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshVideoLayout();
        ScheduleVideoLayoutRefresh();
    }

    private void VideoHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshVideoLayout();
        ScheduleVideoLayoutRefresh();
    }

    private void VideoCanvasResize(object? sender, EventArgs e)
    {
        RefreshVideoLayout();
        ScheduleVideoLayoutRefresh();
    }

    private void VideoPanelResize(object? sender, EventArgs e)
    {
        RefreshVideoLayout();
        ScheduleVideoLayoutRefresh();
    }

    private void VideoLayoutRefreshTimerTick(object? sender, EventArgs e)
    {
        videoLayoutRefreshTimer.Stop();
        RefreshVideoLayout();
    }

    private void RefreshVideoLayout()
    {
        if (isDisposed)
        {
            return;
        }

        VideoFormsHost.InvalidateMeasure();
        VideoFormsHost.InvalidateArrange();
        VideoFormsHost.InvalidateVisual();
        if (VideoFormsHost.IsLoaded)
        {
            VideoFormsHost.UpdateLayout();
        }

        videoCanvas.Invalidate();
        videoPanel.Invalidate();
        TryUpdateSourceVideoSize();
        LayoutVideoPanel();
        ApplyVideoFitMode(GetVideoOutputSize());
    }

    private void ScheduleVideoLayoutRefresh()
    {
        videoLayoutRefreshTimer.Stop();
        videoLayoutRefreshTimer.Start();
    }

    private System.Drawing.Size GetVideoOutputSize()
    {
        System.Drawing.Size panelSize = videoPanel.ClientSize;
        if (panelSize.Width > 0 && panelSize.Height > 0)
        {
            return panelSize;
        }

        double width = Math.Max(0, VideoHost.ActualWidth);
        double height = Math.Max(0, VideoHost.ActualHeight);
        if (width <= 0 || height <= 0)
        {
            return System.Drawing.Size.Empty;
        }

        PresentationSource? source = PresentationSource.FromVisual(VideoHost);
        if (source?.CompositionTarget != null)
        {
            var transform = source.CompositionTarget.TransformToDevice;
            width *= transform.M11;
            height *= transform.M22;
        }

        return new System.Drawing.Size(
            Math.Max(1, (int)Math.Round(width)),
            Math.Max(1, (int)Math.Round(height)));
    }

    private void LayoutVideoPanel()
    {
        System.Drawing.Size canvasSize = videoCanvas.ClientSize;
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
        {
            return;
        }

        System.Drawing.Rectangle bounds = GetVideoPanelBounds(canvasSize);
        if (videoPanel.Bounds != bounds)
        {
            videoPanel.Bounds = bounds;
        }
    }

    private System.Drawing.Rectangle GetVideoPanelBounds(System.Drawing.Size canvasSize)
    {
        if (activeSourceVideoWidth <= 0 || activeSourceVideoHeight <= 0)
        {
            return new System.Drawing.Rectangle(0, 0, canvasSize.Width, canvasSize.Height);
        }

        double scale = Math.Min(
            (double)canvasSize.Width / activeSourceVideoWidth,
            (double)canvasSize.Height / activeSourceVideoHeight);
        if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return new System.Drawing.Rectangle(0, 0, canvasSize.Width, canvasSize.Height);
        }

        int width = Math.Max(1, (int)Math.Round(activeSourceVideoWidth * scale));
        int height = Math.Max(1, (int)Math.Round(activeSourceVideoHeight * scale));
        int left = Math.Max(0, (canvasSize.Width - width) / 2);
        int top = Math.Max(0, (canvasSize.Height - height) / 2);
        return new System.Drawing.Rectangle(left, top, width, height);
    }

    private bool TryUpdateSourceVideoSize()
    {
        if (mediaPlayer == null)
        {
            return false;
        }

        try
        {
            uint width = 0;
            uint height = 0;
            if (!mediaPlayer.Size(0, ref width, ref height) || width == 0 || height == 0)
            {
                return false;
            }

            int videoWidth = width > int.MaxValue ? int.MaxValue : (int)width;
            int videoHeight = height > int.MaxValue ? int.MaxValue : (int)height;
            if (videoWidth == activeSourceVideoWidth && videoHeight == activeSourceVideoHeight)
            {
                return false;
            }

            activeSourceVideoWidth = videoWidth;
            activeSourceVideoHeight = videoHeight;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    private void ApplyVideoFitMode(System.Drawing.Size size)
    {
        if (mediaPlayer == null || size.Width <= 0 || size.Height <= 0 || hasAppliedVideoFitMode)
        {
            return;
        }

        mediaPlayer.Scale = 0;
        mediaPlayer.AspectRatio = null;
        mediaPlayer.CropGeometry = null;
        hasAppliedVideoFitMode = true;
    }

    private void EnsureVideoPanelHandle()
    {
        _ = videoPanel.Handle;
    }

    private static List<string> SelectPreviewUrls(IEnumerable<string?> values)
    {
        List<string> urls = [];

        foreach (string? value in values)
        {
            AddPreviewUrl(urls, value);
        }

        return urls;
    }

    private static List<string> SelectPreviewUrls(params string?[] values)
    {
        return SelectPreviewUrls((IEnumerable<string?>)values);
    }

    private static void AddPreviewUrl(List<string> urls, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        if (!urls.Any(url => url.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            urls.Add(trimmed);
        }
    }

    private static bool ShouldResolveFreshStream(string roomUrl)
    {
        return Uri.TryCreate(roomUrl, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
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
        System.Text.StringBuilder current = new();

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

    private static void AddHeaderPart(List<string> parts, System.Text.StringBuilder current)
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

    private void EmbeddedPreviewWindowClosed(object? sender, EventArgs e)
    {
        DisposePlayer();
    }

    private void DisposePlayer()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        playbackCancellation.Cancel();
        videoLayoutRefreshTimer.Stop();
        DisposeCurrentSession();
        playbackCancellation.Dispose();
        videoCanvas.Resize -= VideoCanvasResize;
        videoPanel.Resize -= VideoPanelResize;
        VideoFormsHost.Child = null;
        videoCanvas.Controls.Remove(videoPanel);
        videoPanel.Dispose();
        videoCanvas.Dispose();
    }

    private void DisposeCurrentSession()
    {
        VlcMediaPlayer? oldMediaPlayer = mediaPlayer;
        VlcMedia? oldMedia = media;
        LibVLC? oldLibVlc = libVlc;

        mediaPlayer = null;
        media = null;
        libVlc = null;

        try
        {
            if (oldMediaPlayer != null)
            {
                oldMediaPlayer.Hwnd = IntPtr.Zero;
            }

            if (oldMediaPlayer != null)
            {
                DetachPlayerEvents(oldMediaPlayer);
                oldMediaPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        oldMedia?.Dispose();
        oldMediaPlayer?.Dispose();
        oldLibVlc?.Dispose();
    }

    private readonly record struct PreviewSource(string Url, string Headers);

    private sealed class PlayerSession(LibVLC libVlc, VlcMediaPlayer mediaPlayer, VlcMedia media) : IDisposable
    {
        public LibVLC LibVlc { get; } = libVlc;

        public VlcMediaPlayer MediaPlayer { get; } = mediaPlayer;

        public VlcMedia Media { get; } = media;

        public void Dispose()
        {
            try
            {
                MediaPlayer.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            Media.Dispose();
            MediaPlayer.Dispose();
            LibVlc.Dispose();
        }
    }

    private sealed class BlackPanel : FormsPanel
    {
        private const int WmEraseBackground = 0x0014;

        public BlackPanel()
        {
            BackColor = System.Drawing.Color.Black;
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WmEraseBackground)
            {
                using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHdc(m.WParam);
                graphics.Clear(System.Drawing.Color.Black);
                m.Result = new IntPtr(1);
                return;
            }

            base.WndProc(ref m);
        }
    }

    private static nint GetWindowLongPtr(IntPtr hWnd, GetWindowLongIndex nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
    }

    private static nint SetWindowLongPtr(IntPtr hWnd, GetWindowLongIndex nIndex, nint dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(IntPtr hWnd, GetWindowLongIndex nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern nint GetWindowLongPtr32(IntPtr hWnd, GetWindowLongIndex nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(IntPtr hWnd, GetWindowLongIndex nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern nint SetWindowLongPtr32(IntPtr hWnd, GetWindowLongIndex nIndex, nint dwNewLong);

    [Flags]
    private enum WindowStyles : uint
    {
        WS_CLIPCHILDREN = 0x02000000,
        WS_CLIPSIBLINGS = 0x04000000,
    }

    private enum GetWindowLongIndex
    {
        GWL_STYLE = -16,
    }
}
