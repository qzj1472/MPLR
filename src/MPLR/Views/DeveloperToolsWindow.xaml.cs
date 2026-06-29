using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using MPLR.Core;

namespace MPLR.Views;

public partial class DeveloperToolsWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
{
    private CancellationTokenSource? networkTestTokenSource;
    private string testUrl = NetworkCapacityTester.DefaultTestUrl;
    private string roomMbpsText = NetworkCapacityTester.DefaultRoomMbps.ToString("0.##");
    private string durationSecondsText = NetworkCapacityTester.DefaultDurationSeconds.ToString();
    private string downloadMbpsText = "-";
    private string theoreticalRoomsText = "-";
    private string safeRoomsText = "-";
    private string networkBytesText = "-";
    private string networkStatusText = "空闲";
    private string processStatusText = string.Empty;
    private string simulationStatusText = string.Empty;
    private Visibility networkPanelVisibility = Visibility.Visible;
    private Visibility processPanelVisibility = Visibility.Collapsed;
    private Visibility simulationPanelVisibility = Visibility.Collapsed;

    public ObservableCollection<ProcessDiagnosticSnapshot> Processes { get; } = [];

    public string TestUrl
    {
        get => testUrl;
        set => SetField(ref testUrl, value);
    }

    public string RoomMbpsText
    {
        get => roomMbpsText;
        set => SetField(ref roomMbpsText, value);
    }

    public string DurationSecondsText
    {
        get => durationSecondsText;
        set => SetField(ref durationSecondsText, value);
    }

    public string DownloadMbpsText
    {
        get => downloadMbpsText;
        set => SetField(ref downloadMbpsText, value);
    }

    public string TheoreticalRoomsText
    {
        get => theoreticalRoomsText;
        set => SetField(ref theoreticalRoomsText, value);
    }

    public string SafeRoomsText
    {
        get => safeRoomsText;
        set => SetField(ref safeRoomsText, value);
    }

    public string NetworkBytesText
    {
        get => networkBytesText;
        set => SetField(ref networkBytesText, value);
    }

    public string NetworkStatusText
    {
        get => networkStatusText;
        set => SetField(ref networkStatusText, value);
    }

    public string ProcessStatusText
    {
        get => processStatusText;
        set => SetField(ref processStatusText, value);
    }

    public string SimulationStatusText
    {
        get => simulationStatusText;
        set => SetField(ref simulationStatusText, value);
    }

    public Visibility NetworkPanelVisibility
    {
        get => networkPanelVisibility;
        set => SetField(ref networkPanelVisibility, value);
    }

    public Visibility ProcessPanelVisibility
    {
        get => processPanelVisibility;
        set => SetField(ref processPanelVisibility, value);
    }

    public Visibility SimulationPanelVisibility
    {
        get => simulationPanelVisibility;
        set => SetField(ref simulationPanelVisibility, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeveloperToolsWindow()
    {
        DataContext = this;
        InitializeComponent();
        RefreshProcesses();
    }

    private async void RunNetworkTestClick(object sender, RoutedEventArgs e)
    {
        if (networkTestTokenSource != null)
        {
            return;
        }

        if (!double.TryParse(RoomMbpsText, out double roomMbps))
        {
            roomMbps = NetworkCapacityTester.DefaultRoomMbps;
        }

        if (!int.TryParse(DurationSecondsText, out int durationSeconds))
        {
            durationSeconds = NetworkCapacityTester.DefaultDurationSeconds;
        }

        networkTestTokenSource = new CancellationTokenSource();
        NetworkStatusText = "测试中...";
        DownloadMbpsText = "-";
        TheoreticalRoomsText = "-";
        SafeRoomsText = "-";
        NetworkBytesText = "-";

        try
        {
            NetworkCapacityTestResult result = await NetworkCapacityTester.TestAsync(TestUrl, roomMbps, durationSeconds, networkTestTokenSource.Token);
            DownloadMbpsText = $"{result.DownloadMbps:0.##} Mbps";
            TheoreticalRoomsText = $"{result.TheoreticalRooms}";
            SafeRoomsText = $"{result.SafeRooms}";
            NetworkBytesText = $"{result.BytesRead / 1024d / 1024d:0.##} MB";
            NetworkStatusText = $"完成：{result.DurationSeconds:0.##} 秒，按每个直播间 {result.RoomMbps:0.##} Mbps 估算，建议保留 20% 余量。";
        }
        catch (OperationCanceledException)
        {
            NetworkStatusText = "已停止";
        }
        catch (Exception exception)
        {
            NetworkStatusText = $"测试失败：{exception.Message}";
        }
        finally
        {
            networkTestTokenSource?.Dispose();
            networkTestTokenSource = null;
        }
    }

    private void CancelNetworkTestClick(object sender, RoutedEventArgs e)
    {
        networkTestTokenSource?.Cancel();
    }

    private void ShowNetworkPanelClick(object sender, RoutedEventArgs e)
    {
        ShowPanel(DeveloperToolPanel.Network);
    }

    private void ShowProcessPanelClick(object sender, RoutedEventArgs e)
    {
        ShowPanel(DeveloperToolPanel.Process);
        RefreshProcesses();
    }

    private void ShowSimulationPanelClick(object sender, RoutedEventArgs e)
    {
        ShowPanel(DeveloperToolPanel.Simulation);
    }

    private void RefreshProcessesClick(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
    }

    private void KillManagedProcessesClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Question("确定清理 MPLR 管理的 ffmpeg/ffprobe/python/node 子进程吗？正在执行的录制或解析会被中断。") != MessageBoxResult.Yes)
        {
            return;
        }

        DevelopmentDiagnostics.KillManagedChildProcesses();
        RefreshProcesses();
        ProcessStatusText = "已清理 MPLR 管理的子进程";
    }

    private void SimulateLiveStartedClick(object sender, RoutedEventArgs e)
    {
        Simulate(NotificationEventKind.LiveStarted, "测试直播间", "开发者工具模拟开播");
    }

    private void SimulateRecordStartedClick(object sender, RoutedEventArgs e)
    {
        Simulate(NotificationEventKind.RecordStarted, "测试直播间", "开发者工具模拟录制开始");
    }

    private void SimulateRecordErrorClick(object sender, RoutedEventArgs e)
    {
        Simulate(NotificationEventKind.RecordError, "测试直播间", "开发者工具模拟录制异常");
    }

    private void SimulateMonitorErrorClick(object sender, RoutedEventArgs e)
    {
        Simulate(NotificationEventKind.MonitorError, "测试直播间", "开发者工具模拟监控异常");
    }

    private void RefreshProcesses()
    {
        Processes.Clear();
        foreach (ProcessDiagnosticSnapshot snapshot in DevelopmentDiagnostics.GetProcessSnapshots())
        {
            Processes.Add(snapshot);
        }

        int childCount = Processes.Count(item => item.IsMplrChild);
        int managedCount = Processes.Count(item => item.IsManaged);
        int externalCount = Processes.Count(item => !item.IsMplrChild && item.ProcessId != Environment.ProcessId);
        ProcessStatusText = $"MPLR 子进程 {childCount} 个，已纳管 {managedCount} 个，外部工具进程 {externalCount} 个";
    }

    private void Simulate(NotificationEventKind kind, string roomName, string detail)
    {
        NotificationCenter.Publish(kind, roomName, detail);
        SimulationStatusText = $"已发送：{kind} / {DateTime.Now:HH:mm:ss}";
        Toast.Success("模拟信号已发送");
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void ShowPanel(DeveloperToolPanel panel)
    {
        NetworkPanelVisibility = panel == DeveloperToolPanel.Network ? Visibility.Visible : Visibility.Collapsed;
        ProcessPanelVisibility = panel == DeveloperToolPanel.Process ? Visibility.Visible : Visibility.Collapsed;
        SimulationPanelVisibility = panel == DeveloperToolPanel.Simulation ? Visibility.Visible : Visibility.Collapsed;
    }

    private enum DeveloperToolPanel
    {
        Network,
        Process,
        Simulation,
    }
}
