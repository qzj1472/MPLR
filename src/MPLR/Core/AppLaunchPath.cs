using System.Diagnostics;
using System.IO;

namespace MPLR.Core;

internal static class AppLaunchPath
{
    public static string ExecutablePath => ResolveExecutablePath();

    public static string IconPath => File.Exists(ExecutablePath) ? ExecutablePath : CurrentProcessPath;

    private static string CurrentProcessPath => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    private static string ResolveExecutablePath()
    {
        string rootCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", $"{AppConfig.DisplayName}.exe"));

        if (File.Exists(rootCandidate))
        {
            return rootCandidate;
        }

        string localCandidate = Path.Combine(AppContext.BaseDirectory, $"{AppConfig.DisplayName}.exe");

        if (File.Exists(localCandidate))
        {
            return localCandidate;
        }

        return CurrentProcessPath;
    }
}
