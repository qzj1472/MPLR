using Fischless.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MPLR.Core;
using MPLR.Extensions;
using MPLR.Threading;
using Velopack;
using Wpf.Ui.Appearance;
using Wpf.Ui.Violeta.Appearance;
using Wpf.Ui.Violeta.Controls;
using Wpf.Ui.Violeta.Win32;

namespace MPLR;

public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        InitializeProcess();
        App app = new();
        app.Run();
    }

    private static void InitializeProcess()
    {
        SystemMenuThemeManager.Apply();
        TaskbarGrouping.SetCurrentProcessAppId();
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        _ = DpiAware.SetProcessDpiAwareness();
        AppPaths.EnsurePortableStorage();
        ConfigurationManager.ConfigurationSerializer = new YamlConfigurationSerializer();
        ConfigurationManager.Setup(AppPaths.ConfigFilePath);
        Locale.Culture = string.IsNullOrWhiteSpace(Configurations.Language.Get()) ? CultureInfo.CurrentUICulture : new CultureInfo(Configurations.Language.Get());
    }

    public App()
    {
        InitializeComponent();
        UserInteractionLogger.Install();

        DispatcherUnhandledException += (object s, DispatcherUnhandledExceptionEventArgs e) =>
        {
            if (e.Exception is NullReferenceException &&
                e.Exception.StackTrace?.Contains("Wpf.Ui.Controls.TitleBar.HwndSourceHook", StringComparison.Ordinal) == true)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            AppSessionLogger.WriteException(e.Exception);
            ExceptionReport.Show(e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                AppSessionLogger.Event("fatal", "exception", exception.GetType().Name, exception.Message, new
                {
                    type = exception.GetType().FullName,
                    exception.Message,
                    stackTrace = exception.ToString(),
                    e.IsTerminating,
                });
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppSessionLogger.Event("error", "exception", "unobserved_task_exception", e.Exception.Message, new
            {
                type = e.Exception.GetType().FullName,
                e.Exception.Message,
                stackTrace = e.Exception.ToString(),
            });
        };

        if (Enum.TryParse(Configurations.Theme.Get(), out ApplicationTheme applicationTheme))
        {
            ThemeManager.Apply(applicationTheme);
        }
        else
        {
            ThemeManager.Apply(ApplicationTheme.Unknown);
        }
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RuntimeHelper.CheckSingleInstance(
            AppConfig.PackName + (Debugger.IsAttached ? "_DEBUG" : string.Empty),
            AppConfig.LegacyPackNames.Select(static name => name + (Debugger.IsAttached ? "_DEBUG" : string.Empty)));
        AppSessionLogger.Start();
        ConfigChangeLogger.Start();
        TrayIconManager.Start();
        NotificationCenter.Start();
        RuntimeResourceLogger.Start();
        _ = ExternalStreamResolver.WarmUpAsync();
        AppUpdater.CheckInBackground();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        GlobalMonitor.Stop();
        GlobalMonitor.StopAllRecorders();
        LowBatteryProtection.Stop();
        NotificationCenter.Stop();
        RuntimeResourceLogger.Stop();
        ChildProcessTracerPeriodicTimer.Default.Stop(killChildren: true);
        ConfigChangeLogger.Stop();
        AppSessionLogger.Stop();
        base.OnExit(e);
    }

}

