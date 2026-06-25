using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiktokLiveRec.Core;

internal static class ExternalStreamResolver
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(75);

    public static string LastError { get; private set; } = string.Empty;

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
        LastError = string.Empty;
        string? normalizedUrl = NormalizeUrl(url);
        string? scriptPath = FindResolverScript();
        string? pythonPath = FindPython();

        AppSessionLogger.Event("info", "business", "stream_resolver_started", "external stream resolver started", new
        {
            originalUrl = url,
            normalizedUrl,
            scriptFound = !string.IsNullOrWhiteSpace(scriptPath),
            pythonFound = !string.IsNullOrWhiteSpace(pythonPath),
            isProxyEnabled = Configurations.IsUseProxy.Get(),
            quality = Configurations.StreamQuality.Get(),
        });

        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            LastError = "empty or invalid url";
            AppSessionLogger.Event("warn", "business", "stream_resolver_rejected", LastError, new
            {
                originalUrl = url,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return null;
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            LastError = "stream resolver script not found";
            AppSessionLogger.Event("error", "business", "stream_resolver_rejected", LastError, new
            {
                normalizedUrl,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return null;
        }

        if (string.IsNullOrWhiteSpace(pythonPath))
        {
            LastError = "python runtime not found";
            AppSessionLogger.Event("error", "business", "stream_resolver_rejected", LastError, new
            {
                normalizedUrl,
                scriptPath,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            });
            return null;
        }

        try
        {
            using Process process = new()
            {
                StartInfo = CreateStartInfo(pythonPath, scriptPath, normalizedUrl),
            };

            process.Start();

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

                LastError = "stream resolver timeout";
                AppSessionLogger.Event("error", "business", "stream_resolver_timeout", LastError, new
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
                exitCode = process.ExitCode,
                elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                stdoutLength = output.Length,
                stderrLength = error.Length,
            });

            if (!string.IsNullOrWhiteSpace(error))
            {
                LastError = TrimError(error);
                Debug.WriteLine(error);
                AppSessionLogger.Event("warn", "system", "process_stderr", LastError, new
                {
                    normalizedUrl,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
            }

            if (process.ExitCode != 0)
            {
                LastError = string.IsNullOrWhiteSpace(LastError)
                    ? $"stream resolver exited with code {process.ExitCode}"
                    : $"stream resolver exited with code {process.ExitCode}: {LastError}";
                AppSessionLogger.Event("error", "business", "stream_resolver_failed", LastError, new
                {
                    normalizedUrl,
                    exitCode = process.ExitCode,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            string? json = FindLastJsonObject(output);
            if (string.IsNullOrWhiteSpace(json))
            {
                LastError = string.IsNullOrWhiteSpace(LastError)
                    ? "stream resolver returned no JSON output"
                    : $"stream resolver returned no JSON output: {LastError}";
                AppSessionLogger.Event("warn", "business", "stream_resolver_no_json", LastError, new
                {
                    normalizedUrl,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            ResolverJsonResult? result = DeserializeResult(json);

            if (result == null)
            {
                LastError = string.IsNullOrWhiteSpace(LastError)
                    ? "stream resolver returned invalid JSON"
                    : $"stream resolver returned invalid JSON: {LastError}";
                AppSessionLogger.Event("warn", "business", "stream_resolver_invalid_json", LastError, new
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
                LastError = string.IsNullOrWhiteSpace(result.Error) ? LastError : result.Error;
                if (string.IsNullOrWhiteSpace(LastError))
                {
                    LastError = "stream resolver returned no room data";
                }

                AppSessionLogger.Event("warn", "business", "stream_resolver_no_room_data", LastError, new
                {
                    normalizedUrl,
                    platform = result.Platform,
                    isLiveStreaming = result.IsLiveStreaming,
                    elapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                });
                return null;
            }

            LastError = string.IsNullOrWhiteSpace(result.Error) ? string.Empty : result.Error;
            AppSessionLogger.Event("info", "business", "stream_resolver_succeeded", "stream resolver returned room data", new
            {
                normalizedUrl,
                platform = result.Platform,
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
            LastError = e.Message;
            Debug.WriteLine(e);
            AppSessionLogger.WriteException(e);
            return null;
        }
    }

    private static ResolverJsonResult? DeserializeResult(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ResolverJsonResult>(json, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException e)
        {
            LastError = e.Message;
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

        foreach (string line in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            string value = line.Trim();
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
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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

        foreach (string rawLine in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
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
            if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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
            Platform = Platform,
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
