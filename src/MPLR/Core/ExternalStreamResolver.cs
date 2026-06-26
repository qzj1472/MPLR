using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using MPLR.Threading;

namespace MPLR.Core;

internal static class ExternalStreamResolver
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(75);
    private static readonly SemaphoreSlim ResolverSemaphore = new(3);
    private static readonly ConcurrentDictionary<string, string> LastErrorsByUrl = new(StringComparer.OrdinalIgnoreCase);

    public static string LastError { get; private set; } = string.Empty;

    public static string GetLastError(string? url)
    {
        foreach (string key in GetErrorKeys(url))
        {
            if (LastErrorsByUrl.TryGetValue(key, out string? error))
            {
                return error ?? string.Empty;
            }
        }

        return LastError;
    }

    public static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string value = url.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        value = NormalizeKnownPlatformUrl(value);

        return Uri.TryCreate(value, UriKind.Absolute, out _) ? value : null;
    }

    private static string NormalizeKnownPlatformUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return value;
        }

        if (uri.Host.Contains("douyu.com", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string roomId = System.Web.HttpUtility.ParseQueryString(uri.Query)["rid"]
                ?? System.Web.HttpUtility.ParseQueryString(uri.Query)["room_id"]
                ?? parts.LastOrDefault(part => !part.Equals("topic", StringComparison.OrdinalIgnoreCase) &&
                                               !part.Equals("room", StringComparison.OrdinalIgnoreCase) &&
                                               !part.Equals("share", StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(roomId))
            {
                return $"https://www.douyu.com/{roomId}";
            }
        }

        if (uri.Host.Contains("twitch.tv", StringComparison.OrdinalIgnoreCase))
        {
            string? channel = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(channel))
            {
                return $"https://www.twitch.tv/{channel}";
            }
        }

        return value;
    }

    public static ISpiderResult? GetResult(string url)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string? normalizedUrl = NormalizeUrl(url);
        string lastError = SetLastError(url, normalizedUrl, string.Empty);
        string? scriptPath = FindResolverScript();
        string? pythonPath = FindPython();

        AppSessionLogger.Event("info", "business", "stream_resolver_started", "external stream resolver started", new
        {
            originalUrl = url,
            normalizedUrl,
            scriptFound = !string.IsNullOrWhiteSpace(scriptPath),
            pythonFound = !string.IsNullOrWhiteSpace(pythonPath),
            pythonPath,
            isProxyEnabled = Configurations.IsUseProxy.Get(),
            quality = Configurations.StreamQuality.Get(),
        });

        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            lastError = SetLastError(url, normalizedUrl, "empty or invalid url");
            AppSessionLogger.Event("warn", "business", "stream_resolver_rejected", lastError, new
            {
                originalUrl = url,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return null;
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            lastError = SetLastError(url, normalizedUrl, "stream resolver script not found");
            AppSessionLogger.Event("warn", "business", "stream_resolver_rejected", lastError, new
            {
                normalizedUrl,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return null;
        }

        if (string.IsNullOrWhiteSpace(pythonPath))
        {
            lastError = SetLastError(url, normalizedUrl, "python runtime not found");
            AppSessionLogger.Event("error", "business", "stream_resolver_rejected", lastError, new
            {
                normalizedUrl,
                scriptPath,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return null;
        }

        try
        {
            using ResolverConcurrencyLease lease = EnterResolverConcurrency();
            using Process process = new()
            {
                StartInfo = CreateStartInfo(pythonPath, scriptPath, normalizedUrl),
            };

            process.Start();
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

                lastError = SetLastError(url, normalizedUrl, "stream resolver timeout");
                AppSessionLogger.Event("error", "business", "stream_resolver_timeout", lastError, new
                {
                    normalizedUrl,
                    pythonPath,
                    scriptPath,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();

            AppSessionLogger.Event("debug", "system", "process_completed", "stream resolver process completed", new
            {
                normalizedUrl,
                pythonPath,
                scriptPath,
                exitCode = process.ExitCode,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                stdoutLength = output.Length,
                stderrLength = error.Length,
            });

            if (!string.IsNullOrWhiteSpace(error))
            {
                lastError = SetLastError(url, normalizedUrl, TrimError(error));
                Debug.WriteLine(error);
                AppSessionLogger.Event("warn", "system", "process_stderr", lastError, new
                {
                    normalizedUrl,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
            }

            if (process.ExitCode != 0)
            {
                lastError = SetLastError(url, normalizedUrl, string.IsNullOrWhiteSpace(lastError)
                    ? $"stream resolver exited with code {process.ExitCode}"
                    : $"stream resolver exited with code {process.ExitCode}: {lastError}");
                AppSessionLogger.Event("error", "business", "stream_resolver_failed", lastError, new
                {
                    normalizedUrl,
                    pythonPath,
                    scriptPath,
                    exitCode = process.ExitCode,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            string? json = FindLastJsonObject(output);
            if (string.IsNullOrWhiteSpace(json))
            {
                lastError = SetLastError(url, normalizedUrl, string.IsNullOrWhiteSpace(lastError)
                    ? "stream resolver returned no JSON output"
                    : $"stream resolver returned no JSON output: {lastError}");
                AppSessionLogger.Event("warn", "business", "stream_resolver_no_json", lastError, new
                {
                    normalizedUrl,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            ResolverJsonResult? result = DeserializeResult(json, out string deserializeError);

            if (result == null)
            {
                if (!string.IsNullOrWhiteSpace(deserializeError))
                {
                    lastError = SetLastError(url, normalizedUrl, deserializeError);
                }

                lastError = SetLastError(url, normalizedUrl, string.IsNullOrWhiteSpace(lastError)
                    ? "stream resolver returned invalid JSON"
                    : $"stream resolver returned invalid JSON: {lastError}");
                AppSessionLogger.Event("warn", "business", "stream_resolver_invalid_json", lastError, new
                {
                    normalizedUrl,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            if (string.IsNullOrWhiteSpace(result.Nickname) &&
                string.IsNullOrWhiteSpace(result.RecordUrl) &&
                string.IsNullOrWhiteSpace(result.FlvUrl) &&
                string.IsNullOrWhiteSpace(result.HlsUrl))
            {
                lastError = SetLastError(url, normalizedUrl, string.IsNullOrWhiteSpace(result.Error) ? lastError : result.Error);
                if (string.IsNullOrWhiteSpace(lastError))
                {
                    lastError = SetLastError(url, normalizedUrl, "stream resolver returned no room data");
                }

                AppSessionLogger.Event("warn", "business", "stream_resolver_no_room_data", lastError, new
                {
                    normalizedUrl,
                    platform = GetPlatform(result.Platform, normalizedUrl),
                    isLiveStreaming = result.IsLiveStreaming,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            lastError = SetLastError(url, normalizedUrl, string.IsNullOrWhiteSpace(result.Error) ? string.Empty : result.Error);
            AppSessionLogger.Event("info", "business", "stream_resolver_succeeded", "stream resolver returned room data", new
            {
                normalizedUrl,
                platform = GetPlatform(result.Platform, normalizedUrl),
                nickname = result.Nickname,
                isLiveStreaming = result.IsLiveStreaming,
                hasRecordUrl = !string.IsNullOrWhiteSpace(result.RecordUrl),
                hasFlvUrl = !string.IsNullOrWhiteSpace(result.FlvUrl),
                hasHlsUrl = !string.IsNullOrWhiteSpace(result.HlsUrl),
                error = result.Error,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return result.ToSpiderResult(normalizedUrl);
        }
        catch (Exception e)
        {
            SetLastError(url, normalizedUrl, e.Message);
            Debug.WriteLine(e);
            AppSessionLogger.WriteException(e);
            return null;
        }
    }

    private static string GetPlatform(string? platform, string? url)
    {
        return string.IsNullOrWhiteSpace(platform) ? PlatformDetector.DetectFromUrl(url) : platform;
    }

    private static ResolverConcurrencyLease EnterResolverConcurrency()
    {
        ResolverSemaphore.Wait();
        return new ResolverConcurrencyLease();
    }

    private sealed class ResolverConcurrencyLease : IDisposable
    {
        public void Dispose()
        {
            ResolverSemaphore.Release();
        }
    }

    private static string SetLastError(string? originalUrl, string? normalizedUrl, string error)
    {
        LastError = error;

        foreach (string key in GetErrorKeys(originalUrl).Concat(GetErrorKeys(normalizedUrl)))
        {
            LastErrorsByUrl[key] = error;
        }

        return error;
    }

    private static IEnumerable<string> GetErrorKeys(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            yield break;
        }

        string key = url.Trim();
        yield return key;

        string? normalizedUrl = NormalizeUrl(key);
        if (!string.IsNullOrWhiteSpace(normalizedUrl) && !normalizedUrl.Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            yield return normalizedUrl;
        }
    }

    private static ResolverJsonResult? DeserializeResult(string json, out string error)
    {
        error = string.Empty;

        try
        {
            return JsonSerializer.Deserialize<ResolverJsonResult>(json, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException e)
        {
            error = e.Message;
            Debug.WriteLine(e);
            return null;
        }
    }

    private static string? FindLastJsonObject(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string value = lines[i].Trim();
            if (!value.StartsWith('{') || !value.EndsWith('}'))
            {
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(value);
                return value;
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static string TrimError(string value)
    {
        string[] lines = value
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(8)
            .ToArray();

        return lines.Length == 0 ? value.Trim() : string.Join(Environment.NewLine, lines);
    }

    private static ProcessStartInfo CreateStartInfo(string pythonPath, string scriptPath, string url)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = pythonPath,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--url");
        startInfo.ArgumentList.Add(url);
        startInfo.ArgumentList.Add("--quality");
        startInfo.ArgumentList.Add(Configurations.StreamQuality.Get());

        string proxy = Configurations.ProxyUrl.Get();
        if (Configurations.IsUseProxy.Get() && !string.IsNullOrWhiteSpace(proxy))
        {
            startInfo.ArgumentList.Add("--proxy");
            startInfo.ArgumentList.Add(proxy);
        }

        string cookieChina = Configurations.CookieChina.Get();
        if (!string.IsNullOrWhiteSpace(cookieChina))
        {
            startInfo.ArgumentList.Add("--cookie-china");
            startInfo.ArgumentList.Add(cookieChina);
        }

        string cookieOversea = Configurations.CookieOversea.Get();
        if (!string.IsNullOrWhiteSpace(cookieOversea))
        {
            startInfo.ArgumentList.Add("--cookie-oversea");
            startInfo.ArgumentList.Add(cookieOversea);
        }

        string? configPath = WriteResolverConfig();
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(configPath);
        }

        return startInfo;
    }

    private static string? WriteResolverConfig()
    {
        Dictionary<string, string> cookies = ParsePlatformCookies(Configurations.PlatformCookies.Get());
        if (cookies.Count == 0)
        {
            return null;
        }

        string directory = Path.Combine(Path.GetTempPath(), AppConfig.PackName, "stream_resolver");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "resolver_config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["cookies"] = cookies,
        }));
        return path;
    }

    private static Dictionary<string, string> ParsePlatformCookies(string value)
    {
        Dictionary<string, string> cookies = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                separator = line.IndexOf(':');
            }

            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            string key = line[..separator].Trim().Trim('"', '\'').ToLowerInvariant();
            string cookie = line[(separator + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(cookie))
            {
                cookies[key] = cookie;
            }
        }

        return cookies;
    }

    private static string? FindResolverScript()
    {
        foreach (string candidate in EnumerateResolverScriptCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateResolverScriptCandidates()
    {
        string baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, "stream_resolver", "resolver.py");
        yield return Path.Combine(baseDirectory, "tools", "stream_resolver", "resolver.py");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "stream_resolver", "resolver.py");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "tools", "stream_resolver", "resolver.py");

        DirectoryInfo? directory = new(baseDirectory);
        while (directory != null)
        {
            yield return Path.Combine(directory.FullName, "tools", "stream_resolver", "resolver.py");
            directory = directory.Parent;
        }
    }

    private static string? FindPython()
    {
        string? configured = Environment.GetEnvironmentVariable("STREAM_RESOLVER_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured) && IsUsablePython(configured))
        {
            return configured;
        }

        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "stream_resolver", ".venv", "Scripts", "python.exe"),
            Path.Combine(baseDirectory, ".venv", "Scripts", "python.exe"),
            Path.Combine(baseDirectory, "python", "python.exe"),
            "python",
            "python3",
        ];

        foreach (string candidate in candidates)
        {
            if (IsUsablePython(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsUsablePython(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !File.Exists(candidate))
        {
            return false;
        }

        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = candidate,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            process.StartInfo.ArgumentList.Add("--version");
            process.Start();
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(e);
            return false;
        }
    }

    private sealed class ResolverJsonResult
    {
        [JsonPropertyName("room_url")]
        public string? RoomUrl { get; set; }

        [JsonPropertyName("is_live_streaming")]
        public bool? IsLiveStreaming { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("avatar_thumb_url")]
        public string? AvatarThumbUrl { get; set; }

        [JsonPropertyName("flv_url")]
        public string? FlvUrl { get; set; }

        [JsonPropertyName("hls_url")]
        public string? HlsUrl { get; set; }

        [JsonPropertyName("record_url")]
        public string? RecordUrl { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        [JsonPropertyName("uid")]
        public string? Uid { get; set; }

        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }

        [JsonPropertyName("bitrate")]
        public string? Bitrate { get; set; }

        [JsonPropertyName("headers")]
        public string? Headers { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        public ExternalSpiderResult ToSpiderResult(string fallbackUrl) => new()
        {
            RoomUrl = string.IsNullOrWhiteSpace(RoomUrl) ? fallbackUrl : RoomUrl,
            IsLiveStreaming = IsLiveStreaming,
            Nickname = Nickname,
            AvatarThumbUrl = AvatarThumbUrl,
            FlvUrl = FlvUrl,
            HlsUrl = HlsUrl,
            RecordUrl = RecordUrl,
            Platform = GetPlatform(Platform, RoomUrl ?? fallbackUrl),
            Title = Title,
            Quality = Quality,
            Uid = Uid,
            Resolution = Resolution,
            Bitrate = Bitrate,
            Headers = Headers,
        };
    }
}

public sealed class ExternalSpiderResult : ISpiderResult
{
    public string? RoomUrl { get; set; }

    public bool? IsLiveStreaming { get; set; }

    public string? Nickname { get; set; }

    public string? AvatarThumbUrl { get; set; }

    public string? FlvUrl { get; set; }

    public string? HlsUrl { get; set; }

    public string? RecordUrl { get; set; }

    public string? Platform { get; set; }

    public string? Title { get; set; }

    public string? Quality { get; set; }

    public string? Uid { get; set; }

    public string? Resolution { get; set; }

    public string? Bitrate { get; set; }

    public string? Headers { get; set; }
}

