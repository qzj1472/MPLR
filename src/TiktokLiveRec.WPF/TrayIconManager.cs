using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using TiktokLiveRec.Core;
using TiktokLiveRec.Extensions;
using TiktokLiveRec.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Violeta.Appearance;
using Wpf.Ui.Violeta.Win32;

namespace TiktokLiveRec;

internal class TrayIconManager
{
    private static TrayIconManager _instance = null!;

    private readonly TrayIconHost _icon = null!;

    private readonly TrayMenuItem? _itemAutoRun = null;

    public bool IsShutdownTriggered { get; private set; } = false;

    private TrayIconManager()
    {
        _icon = new TrayIconHost()
        {
            ToolTipText = AppConfig.DisplayName,
            Menu =
            [
                new TrayMenuItem()
                {
                    Header = AppConfig.DisplayName,
                    IsEnabled = false,
                },
                new TrayMenuItem()
                {
                    Header = $"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}",
                    IsEnabled = false,
                },
                new TraySeparator(),
                new TrayMenuItem()
                {
                   Header = "TrayMenuShowMainWindow".Tr(),
                   Tag = "TrayMenuShowMainWindow",
                   Command = new RelayCommand(() =>
                   {
                        Application.Current.MainWindow.Show();
                        Application.Current.MainWindow.Activate();
                        Interop.RestoreWindow(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                    }),
                },
                new TrayMenuItem()
                {
                    Header = "TrayMenuOpenSettings".Tr(),
                    Tag = "TrayMenuOpenSettings",
                    Command = new RelayCommand(() =>
                    {
                        foreach (Window win in Application.Current.Windows.OfType<SettingsWindow>())
                        {
                        win.Close();
                        }

                        _ = new SettingsWindow()
                        {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        }.ShowDialog();
                    }),
                },
                _itemAutoRun = new TrayMenuItem()
                {
                    Header = "TrayMenuAutoRun".Tr(),
                    Tag = "TrayMenuAutoRun",
                    Command = new RelayCommand(() =>
                    {
                        if (AutoStartupHelper.IsAutorun())
                        {
                            AutoStartupHelper.RemoveAutorunShortcut();
                        }
                        else
                        {
                            AutoStartupHelper.CreateAutorunShortcut();
                        }
                    }),
                },
                new TrayMenuItem()
                {
                    Header = "TrayMenuRestart".Tr(),
                    Tag = "TrayMenuRestart",
                    Command = new RelayCommand(() =>
                    {
                        if (GlobalMonitor.RoomStatus.Values.ToArray().Any(roomStatus => roomStatus.RecordStatus == RecordStatus.Recording))
                        {
                            if (MessageBox.Question("SureOnRecording".Tr()) == MessageBoxResult.Yes)
                            {
                                RuntimeHelper.Restart(forced: true);
                            }
                        }
                        else
                        {
                            RuntimeHelper.Restart(forced: true);
                        }
                    }),
                },
                new TrayMenuItem()
                {
                    Header = "TrayMenuExit".Tr(),
                    Tag = "TrayMenuExit",
                    Command = new RelayCommand(() =>
                    {
                        if (GlobalMonitor.RoomStatus.Values.ToArray().Any(roomStatus => roomStatus.RecordStatus == RecordStatus.Recording))
                        {
                            if (MessageBox.Question("SureOnRecording".Tr()) == MessageBoxResult.Yes)
                            {
                                IsShutdownTriggered = true;
                                Application.Current.Shutdown();
                            }
                        }
                        else
                        {
                            IsShutdownTriggered = true;
                            Application.Current.Shutdown();
                        }
                    }),
                },
            ],
        };
        UpdateTrayIcon();

        _icon.RightDown += (_, _) =>
        {
            _itemAutoRun.IsChecked = AutoStartupHelper.IsAutorun();
        };

        _icon.LeftDoubleClick += (_, _) =>
        {
            if (Application.Current.MainWindow.IsVisible)
            {
                Application.Current.MainWindow.Hide();
            }
            else
            {
                Application.Current.MainWindow.Show();
                Application.Current.MainWindow.Activate();
                Interop.RestoreWindow(new WindowInteropHelper(Application.Current.MainWindow).Handle);
            }
        };

        Locale.CultureChanged += (_, _) =>
        {
            foreach (ITrayMenuItemBase item in _icon.Menu.Items)
            {
                if (item.Tag is string trKey)
                {
                    item.Header = trKey.Tr();
                }
            }
        };

        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Configurations.Theme.Get()))
            {
                ThemeManager.Apply(ApplicationTheme.Unknown);
            }
            UpdateTrayIcon();
        };
    }

    public static TrayIconManager GetInstance()
    {
        return _instance ??= new TrayIconManager();
    }

    public static void Start()
    {
        _ = GetInstance();
    }

    public void RequestShutdown()
    {
        if (GlobalMonitor.RoomStatus.Values.ToArray().Any(roomStatus => roomStatus.RecordStatus == RecordStatus.Recording))
        {
            if (MessageBox.Question("SureOnRecording".Tr()) != MessageBoxResult.Yes)
            {
                return;
            }
        }

        IsShutdownTriggered = true;
        Application.Current.Shutdown();
    }

    public void UpdateTrayIcon()
    {
        try
        {
            _icon.Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName!)!.Handle;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
}
