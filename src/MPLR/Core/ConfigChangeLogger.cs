using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MPLR.Core;

internal static partial class ConfigChangeLogger
{
    private static readonly object LockObject = new();
    private static FileSystemWatcher? watcher;
    private static string? lastSnapshot;
    private static string? lastHash;

    public static void Start()
    {
        if (watcher is not null)
        {
            return;
        }

        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        lastSnapshot = ReadSanitizedSnapshot(AppPaths.ConfigFilePath);
        lastHash = ComputeHash(lastSnapshot);
        watcher = new FileSystemWatcher(AppPaths.ConfigDirectory)
        {
            Filter = "*.y*ml",
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        watcher.Changed += (_, e) => LogChange("changed", e.FullPath);
        watcher.Created += (_, e) => LogChange("created", e.FullPath);
        watcher.Deleted += (_, e) => LogChange("deleted", e.FullPath);
        watcher.Renamed += (_, e) => LogChange("renamed", e.FullPath);

        AppSessionLogger.Event("info", "storage", "config_watcher_started", "config watcher started", new
        {
            directory = AppPaths.ConfigDirectory,
            file = AppPaths.ConfigFilePath,
            initialHash = lastHash,
            initialSnapshot = Limit(lastSnapshot),
        });
    }

    public static void Stop()
    {
        watcher?.Dispose();
        watcher = null;
    }

    private static void LogChange(string action, string path)
    {
        lock (LockObject)
        {
            Thread.Sleep(50);
            string? beforeSnapshot = lastSnapshot;
            string? beforeHash = lastHash;
            string? afterSnapshot = ReadSanitizedSnapshot(AppPaths.ConfigFilePath);
            string? afterHash = ComputeHash(afterSnapshot);

            if (beforeHash == afterHash && action == "changed")
            {
                return;
            }

            lastSnapshot = afterSnapshot;
            lastHash = afterHash;

            AppSessionLogger.Event("info", "storage", $"config_{action}", "config file changed", new
            {
                path,
                configPath = AppPaths.ConfigFilePath,
                beforeHash,
                afterHash,
                beforeExists = beforeSnapshot != null,
                afterExists = afterSnapshot != null,
                beforeSnapshot = Limit(beforeSnapshot),
                afterSnapshot = Limit(afterSnapshot),
            });
        }
    }

    private static string? ReadSanitizedSnapshot(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream, Encoding.UTF8);
            return SensitiveLineRegex().Replace(reader.ReadToEnd(), "$1: [redacted]");
        }
        catch (IOException e)
        {
            AppSessionLogger.WriteException(e);
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            AppSessionLogger.WriteException(e);
            return null;
        }
    }

    private static string? ComputeHash(string? value)
    {
        return value == null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string? Limit(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= 8000 ? value : value[..8000] + $"...[truncated:{value.Length}]";
    }

    [GeneratedRegex(@"(?im)^(\s*(?:.*password.*|.*cookie.*|.*token.*|.*secret.*|.*authorization.*|.*platformcookies.*|.*cookiechina.*|.*cookieoversea.*)\s*):.*$")]
    private static partial Regex SensitiveLineRegex();
}

