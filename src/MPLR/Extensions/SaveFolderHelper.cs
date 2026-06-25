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

    public static string GetRecordFolder(string? settingsFolder, RecorderStartInfo startInfo, DateTime now)
    {
        string saveFolder = GetSaveFolder(settingsFolder);
        string author = SanitizeFolderName(startInfo.NickName);
        string platform = string.IsNullOrWhiteSpace(startInfo.Platform)
            ? "Unknown"
            : SanitizeFolderName(startInfo.Platform);
        string year = now.ToString("yyyy");
        string month = now.ToString("MM");

        return Configurations.SaveFolderPathLevel.Get() switch
        {
            1 => Path.Combine(saveFolder, platform, author, year, month),
            2 => saveFolder,
            _ => Path.Combine(saveFolder, author, year, month),
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
