using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using MPLR.Core;

namespace MPLR.Extensions;

internal static class RuntimeHelper
{
    public static void CheckSingleInstance(string instanceName, IEnumerable<string>? legacyInstanceNames = null, Action<bool> callback = null!)
    {
        foreach (string legacyInstanceName in legacyInstanceNames ?? [])
        {
            if (TryActivateExistingInstance(legacyInstanceName, callback))
            {
                return;
            }
        }

        if (TryActivateExistingInstance(instanceName, callback))
        {
            return;
        }

        StartInstanceMonitor(instanceName, callback);
    }

    private static bool TryActivateExistingInstance(string instanceName, Action<bool> callback)
    {
        EventWaitHandle? handle;

        try
        {
            handle = EventWaitHandle.OpenExisting(instanceName);
            AppSessionLogger.Write($"single instance already running: {instanceName}");
            handle.Set();
            callback?.Invoke(false);
            Environment.Exit(0xFFFF);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    private static void StartInstanceMonitor(string instanceName, Action<bool> callback)
    {
        EventWaitHandle handle = new(false, EventResetMode.AutoReset, instanceName);
        callback?.Invoke(true);

        _ = Task.Factory.StartNew(() =>
        {
            while (handle.WaitOne())
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    Application.Current.MainWindow?.Activate();
                    Application.Current.MainWindow?.Show();
                    Interop.RestoreWindow(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                });
            }
        }, TaskCreationOptions.LongRunning).ConfigureAwait(false);
    }

    public static string ReArguments()
    {
        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        for (int i = default; i < args.Length; i++)
        {
            args[i] = $@"""{args[i]}""";
        }
        return string.Join(" ", args);
    }

    public static void Restart(string fileName = null!, string dir = null!, string args = null!, int? exitCode = null, bool forced = false)
    {
        _ = args;

        try
        {
            AppSessionLogger.Write("application restart requested");
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = fileName ?? GetExecutablePath(),
                    WorkingDirectory = dir ?? Environment.CurrentDirectory,
                    UseShellExecute = true,
                },
            };
            process.Start();
        }
        catch (Win32Exception)
        {
            return;
        }
        if (forced)
        {
            Process.GetCurrentProcess().Kill();
        }
        Environment.Exit(exitCode ?? 'r' + 'e' + 's' + 't' + 'a' + 'r' + 't');

        static string GetExecutablePath()
        {
            string fileName = AppDomain.CurrentDomain.FriendlyName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName += ".exe";
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }
    }
}

