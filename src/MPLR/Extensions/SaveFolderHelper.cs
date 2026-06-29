using System.Diagnostics;
using MPLR.Core;

namespace MPLR.Extensions;

internal static class SaveFolderHelper
{
    public static string GetSaveFolder(string? settingsFolder = null)
    {
        if (string.IsNullOrWhiteSpace(settingsFolder))
        {
            const string path = "downloads";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return Path.GetFullPath(path);
        }

        try
        {
            if (!Directory.Exists(settingsFolder))
            {
                Directory.CreateDirectory(settingsFolder);
            }

            return Path.GetFullPath(settingsFolder);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return GetSaveFolder(null);
        }
    }

    public static string GetRecordFolder(string? settingsFolder, RecorderStartInfo startInfo, DateTime now, int? pathLevel = null)
    {
        string saveFolder = GetSaveFolder(settingsFolder);
        string author = SanitizeFolderName(startInfo.NickName);
        string platform = string.IsNullOrWhiteSpace(startInfo.Platform)
            ? "Unknown"
            : SanitizeFolderName(startInfo.Platform);
        string time = now.ToString("yyyy-MM");

        return Math.Clamp(pathLevel ?? Configurations.SaveFolderPathLevel.Get(), 0, 1) switch
        {
            1 => Path.Combine(saveFolder, platform, author, time),
            _ => Path.Combine(saveFolder, author, time),
        };
    }

    private static string SanitizeFolderName(string value)
    {
        string name = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        name = name.TrimEnd('.');

        return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
    }
}
