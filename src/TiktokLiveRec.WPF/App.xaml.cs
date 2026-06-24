using Fischless.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TiktokLiveRec.Extensions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Violeta.Appearance;
using Wpf.Ui.Violeta.Controls;
using Wpf.Ui.Violeta.Win32;

namespace TiktokLiveRec;

public partial class App : Application
{
    static App()
    {
        SystemMenuThemeManager.Apply();
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        _ = DpiAware.SetProcessDpiAwareness();
        MigrateLegacyAppData();
        ConfigurationManager.ConfigurationSerializer = new YamlConfigurationSerializer();
        ConfigurationManager.Setup(ConfigurationSpecialPath.GetPath("config.yaml", AppConfig.PackName));
        Locale.Culture = string.IsNullOrWhiteSpace(Configurations.Language.Get()) ? CultureInfo.CurrentUICulture : new CultureInfo(Configurations.Language.Get());
    }

    public App()
    {
        InitializeComponent();

        DispatcherUnhandledException += (object s, DispatcherUnhandledExceptionEventArgs e) =>
        {
            if (e.Exception is NullReferenceException &&
                e.Exception.StackTrace?.Contains("Wpf.Ui.Controls.TitleBar.HwndSourceHook", StringComparison.Ordinal) == true)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            ExceptionReport.Show(e.Exception);
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

        RuntimeHelper.CheckSingleInstance(AppConfig.PackName + (Debugger.IsAttached ? "_DEBUG" : string.Empty));
        TrayIconManager.Start();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }

    private static void MigrateLegacyAppData()
    {
        string legacyConfig = ConfigurationSpecialPath.GetPath("config.yaml", AppConfig.LegacyPackName);
        string currentConfig = ConfigurationSpecialPath.GetPath("config.yaml", AppConfig.PackName);
        string? legacyDirectory = Path.GetDirectoryName(legacyConfig);
        string? currentDirectory = Path.GetDirectoryName(currentConfig);

        if (string.IsNullOrWhiteSpace(legacyDirectory) ||
            string.IsNullOrWhiteSpace(currentDirectory) ||
            !Directory.Exists(legacyDirectory) ||
            File.Exists(currentConfig))
        {
            return;
        }

        CopyDirectory(legacyDirectory, currentDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDirectory, targetDirectory));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string target = file.Replace(sourceDirectory, targetDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, false);
        }
    }
}
