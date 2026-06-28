using Fischless.Configuration;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MPLR.Core;

public static class ConfigImporter
{
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Configurations.Language),
        nameof(Configurations.Theme),
        nameof(Configurations.DisplayScale),
        nameof(Configurations.IsOffRemindCloseToTray),
        nameof(Configurations.Rooms),
        nameof(Configurations.IsUseStatusTray),
        nameof(Configurations.IsSessionLogEnabled),
        nameof(Configurations.RoutineInterval),
        nameof(Configurations.RoutineScheduleMode),
        nameof(Configurations.RoutineScheduleDays),
        nameof(Configurations.RoutineScheduleStartHour),
        nameof(Configurations.RoutineScheduleStartMinute),
        nameof(Configurations.RoutineScheduleEndHour),
        nameof(Configurations.RoutineScheduleEndMinute),
        nameof(Configurations.IsMonitorRunning),
        nameof(Configurations.IsToNotify),
        nameof(Configurations.NotifySummaryIntervalMinutes),
        nameof(Configurations.IsToNotifyWithSystem),
        nameof(Configurations.IsToNotifyWithMusic),
        nameof(Configurations.ToNotifyWithMusicPath),
        nameof(Configurations.IsToNotifyWithEmail),
        nameof(Configurations.ToNotifyWithEmailSmtp),
        nameof(Configurations.ToNotifyWithEmailUserName),
        nameof(Configurations.ToNotifyWithEmailPassword),
        nameof(Configurations.IsToNotifyGotoRoomUrl),
        nameof(Configurations.IsToNotifyGotoRoomUrlAndMute),
        nameof(Configurations.IsToRecord),
        nameof(Configurations.StreamQuality),
        nameof(Configurations.RecordFormat),
        nameof(Configurations.IsRemoveTs),
        nameof(Configurations.IsToSegment),
        nameof(Configurations.SegmentTime),
        nameof(Configurations.SegmentTimeUnit),
        nameof(Configurations.SaveFolder),
        nameof(Configurations.SaveFolderDistinguishedByAuthors),
        nameof(Configurations.SaveFolderPathLevel),
        nameof(Configurations.SaveFileNameRule),
        nameof(Configurations.SaveFileNameCustomRule),
        nameof(Configurations.Player),
        nameof(Configurations.IsPlayerRect),
        nameof(Configurations.IsUseKeepAwake),
        nameof(Configurations.IsUseAutoShutdown),
        nameof(Configurations.AutoShutdownTime),
        nameof(Configurations.IsUseProxy),
        nameof(Configurations.ProxyUrl),
        nameof(Configurations.CookieChina),
        nameof(Configurations.CookieOversea),
        nameof(Configurations.PlatformCookies),
        nameof(Configurations.UserAgent),
    };

    public static string Import(string sourcePath)
    {
        Validate(sourcePath);

        string targetPath = ConfigurationManager.FilePath;
        string? targetDirectory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string backupPath = GetBackupPath(targetPath);

        if (File.Exists(targetPath))
        {
            File.Copy(targetPath, backupPath, false);
        }

        File.Copy(sourcePath, targetPath, true);
        ConfigurationManager.Setup(targetPath);

        return backupPath;
    }

    public static string Export(string targetPath)
    {
        string sourcePath = ConfigurationManager.FilePath;
        string? targetDirectory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        ConfigurationManager.Save();
        File.Copy(sourcePath, targetPath, true);

        return targetPath;
    }

    public static string[] Reset()
    {
        AppSessionLogger.Write("config reset requested");
        List<string> backupPaths = [];

        try
        {
            ResetFile(ConfigurationManager.FilePath, backupPaths);

            foreach (string file in AppPaths.GetLegacyConfigFiles())
            {
                ResetFile(file, backupPaths);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppSessionLogger.WriteException(exception);
            throw;
        }

        AppSessionLogger.Write($"config reset completed; backups={string.Join("|", backupPaths)}");

        return [.. backupPaths];
    }

    private static void ResetFile(string targetPath, List<string> backupPaths)
    {
        if (!File.Exists(targetPath))
        {
            return;
        }

        string backupPath = GetBackupPath(targetPath);
        AppSessionLogger.Write($"config reset deleting {targetPath}; backup={backupPath}");
        File.Copy(targetPath, backupPath, false);
        File.Delete(targetPath);
        backupPaths.Add(backupPath);
    }

    private static void Validate(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Config file was not found.", sourcePath);
        }

        string extension = Path.GetExtension(sourcePath);

        if (!extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only YAML config files are supported.");
        }

        using StreamReader reader = File.OpenText(sourcePath);
        YamlStream yaml = new();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0 ||
            yaml.Documents[0].RootNode is not YamlMappingNode root ||
            !root.Children.Keys.OfType<YamlScalarNode>().Any(key => key.Value is not null && KnownKeys.Contains(key.Value)))
        {
            throw new InvalidDataException("The YAML file does not look like a supported app config.");
        }
    }

    private static string GetBackupPath(string targetPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(targetPath);
        string extension = Path.GetExtension(targetPath);
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        Directory.CreateDirectory(AppPaths.ConfigDirectory);

        for (int index = 1; ; index++)
        {
            string suffix = index == 1 ? string.Empty : $"-{index}";
            string backupPath = Path.Combine(AppPaths.ConfigDirectory, $"{fileName}.bak-{timestamp}{suffix}{extension}");

            if (!File.Exists(backupPath))
            {
                return backupPath;
            }
        }
    }
}

