using Fischless.Configuration;
using System.IO;

namespace TiktokLiveRec.Core;

internal static class AppPaths
{
    public static string RootDirectory => Path.GetFullPath(AppContext.BaseDirectory);

    public static string ConfigDirectory => Path.Combine(RootDirectory, "config");

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.yaml");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static string CacheDirectory => Path.Combine(RootDirectory, "cache");

    public static string[] LegacyConfigDirectories =>
    [
        Path.GetDirectoryName(ConfigurationSpecialPath.GetPath("config.yaml", AppConfig.PackName)) ?? string.Empty,
        Path.GetDirectoryName(ConfigurationSpecialPath.GetPath("config.yaml", AppConfig.LegacyPackName)) ?? string.Empty,
    ];

    public static void EnsurePortableStorage()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);

        foreach (string directory in LegacyConfigDirectories.Where(static directory => !string.IsNullOrWhiteSpace(directory)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            MoveConfigFiles(directory);
            MoveLogFiles(Path.Combine(directory, "logs"));
        }
    }

    public static IEnumerable<string> GetLegacyConfigFiles()
    {
        foreach (string directory in LegacyConfigDirectories.Where(static directory => !string.IsNullOrWhiteSpace(directory)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "config*.yaml", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(directory, "config*.yml", SearchOption.TopDirectoryOnly)))
            {
                yield return file;
            }
        }
    }

    private static void MoveConfigFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string sourcePath in Directory.EnumerateFiles(directory, "config*.yaml", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(directory, "config*.yml", SearchOption.TopDirectoryOnly)))
        {
            if (Path.GetFullPath(sourcePath).StartsWith(ConfigDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string targetPath = GetAvailableConfigPath(Path.GetFileName(sourcePath));

            try
            {
                File.Move(sourcePath, targetPath);
            }
            catch (IOException)
            {
                File.Copy(sourcePath, targetPath, false);
                File.Delete(sourcePath);
            }
        }
    }

    private static void MoveLogFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string sourcePath in Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly))
        {
            string targetPath = GetAvailableLogPath(Path.GetFileName(sourcePath));

            try
            {
                File.Move(sourcePath, targetPath);
            }
            catch (IOException)
            {
                File.Copy(sourcePath, targetPath, false);
                File.Delete(sourcePath);
            }
        }
    }

    private static string GetAvailableConfigPath(string fileName)
    {
        string targetPath = Path.Combine(ConfigDirectory, fileName);

        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        for (int index = 1; ; index++)
        {
            string suffix = index == 1 ? string.Empty : $"-{index}";
            targetPath = Path.Combine(ConfigDirectory, $"{name}.migrated-{timestamp}{suffix}{extension}");

            if (!File.Exists(targetPath))
            {
                return targetPath;
            }
        }
    }

    private static string GetAvailableLogPath(string fileName)
    {
        string targetPath = Path.Combine(LogsDirectory, fileName);

        if (!File.Exists(targetPath))
        {
            return targetPath;
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        for (int index = 1; ; index++)
        {
            string suffix = index == 1 ? string.Empty : $"-{index}";
            targetPath = Path.Combine(LogsDirectory, $"{name}.migrated-{timestamp}{suffix}{extension}");

            if (!File.Exists(targetPath))
            {
                return targetPath;
            }
        }
    }
}
