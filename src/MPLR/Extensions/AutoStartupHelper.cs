using Microsoft.Win32;
using System.Diagnostics;

namespace MPLR.Extensions;

internal static class AutoStartupHelper
{
    public static bool IsAutorun()
    {
        string launchCommand = $"\"{Process.GetCurrentProcess().MainModule?.FileName!}\" /autorun";
        return EnumerateAutorunKeys().Any(keyName => RegistyAutoRunHelper.IsEnabled(keyName, launchCommand));
    }

    public static void RemoveAutorunShortcut()
    {
        foreach (string keyName in EnumerateAutorunKeys())
        {
            RegistyAutoRunHelper.Disable(keyName);
        }
    }

    public static void CreateAutorunShortcut()
    {
        foreach (string keyName in AppConfig.LegacyDisplayNames.Concat(AppConfig.LegacyPackNames))
        {
            RegistyAutoRunHelper.Disable(keyName);
        }

        RegistyAutoRunHelper.Enable(AppConfig.DisplayName, $"\"{Process.GetCurrentProcess().MainModule?.FileName!}\" /autorun");
    }

    private static IEnumerable<string> EnumerateAutorunKeys()
    {
        yield return AppConfig.DisplayName;
        yield return AppConfig.PackName;

        foreach (string keyName in AppConfig.LegacyDisplayNames.Concat(AppConfig.LegacyPackNames))
        {
            yield return keyName;
        }
    }
}

file static class RegistyAutoRunHelper
{
    private const string RunLocation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static void Enable(string keyName, string launchCommand)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunLocation);
        key?.SetValue(keyName, launchCommand);
    }

    public static bool IsEnabled(string keyName, string launchCommand)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunLocation);

        if (key == null)
        {
            return false;
        }

        string? value = (string?)key.GetValue(keyName);

        if (value == null)
        {
            return false;
        }

        return value == launchCommand;
    }

    public static void Disable(string keyName, string launchCommand = null!)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunLocation);

        _ = launchCommand;
        if (key == null)
        {
            return;
        }

        if (key.GetValue(keyName) != null)
        {
            key.DeleteValue(keyName);
        }
    }

    public static void SetEnabled(bool enable, string keyName, string launchCommand)
    {
        if (enable)
        {
            Enable(keyName, launchCommand);
        }
        else
        {
            Disable(keyName);
        }
    }
}

