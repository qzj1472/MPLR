using System.IO;
using System.IO.Compression;

namespace MPLR.Core;

internal static class LogExporter
{
    public static string ExportLatest(string targetDirectory)
    {
        string[] files = GetLatestSessionFiles();
        if (files.Length == 0)
        {
            throw new FileNotFoundException("没有找到可导出的日志文件。");
        }

        return CreateArchive(targetDirectory, $"MPLR_logs_latest_{DateTime.Now:yyyyMMdd_HHmmss}.zip", files);
    }

    public static string ExportAll(string targetDirectory)
    {
        string[] files = Directory.Exists(AppPaths.LogsDirectory)
            ? Directory.GetFiles(AppPaths.LogsDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        if (files.Length == 0)
        {
            throw new FileNotFoundException("没有找到可导出的日志文件。");
        }

        return CreateArchive(targetDirectory, $"MPLR_logs_all_{DateTime.Now:yyyyMMdd_HHmmss}.zip", files);
    }

    private static string[] GetLatestSessionFiles()
    {
        if (!Directory.Exists(AppPaths.LogsDirectory))
        {
            return [];
        }

        string? latest = Directory.GetFiles(AppPaths.LogsDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .Where(file => !file.EndsWith(".error.log", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(latest))
        {
            return [];
        }

        string errorLog = Path.Combine(
            Path.GetDirectoryName(latest)!,
            Path.GetFileNameWithoutExtension(latest) + ".error.log");

        return File.Exists(errorLog) ? [latest, errorLog] : [latest];
    }

    private static string CreateArchive(string targetDirectory, string archiveName, IReadOnlyList<string> files)
    {
        Directory.CreateDirectory(targetDirectory);
        string archivePath = GetAvailablePath(Path.Combine(targetDirectory, archiveName));

        using FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        foreach (string file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal);
            using Stream entryStream = entry.Open();
            using FileStream fileStream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            fileStream.CopyTo(entryStream);
        }

        return archivePath;
    }

    private static string GetAvailablePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 1; i < 1000; i++)
        {
            string candidate = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name}_{Guid.NewGuid():N}{extension}");
    }
}
