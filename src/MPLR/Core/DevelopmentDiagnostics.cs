using System.Diagnostics;
using MPLR.Threading;

namespace MPLR.Core;

public sealed record ProcessDiagnosticSnapshot(
    int ProcessId,
    string ProcessName,
    int? ParentProcessId,
    bool IsMplrChild,
    bool IsManaged,
    double WorkingSetMb,
    string StartedAt,
    string State);

internal static class DevelopmentDiagnostics
{
    private static readonly string[] ToolProcessNames = ["ffmpeg", "ffprobe", "python", "python3", "node"];

    public static ProcessDiagnosticSnapshot[] GetProcessSnapshots()
    {
        HashSet<int> tracedIds = ChildProcessTracerPeriodicTimer.Default.GetTracedProcessIds();
        List<ProcessDiagnosticSnapshot> snapshots = [];

        AddProcessSnapshot(snapshots, Process.GetCurrentProcess(), tracedIds, "MPLR 主进程");

        foreach (string processName in ToolProcessNames)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    AddProcessSnapshot(snapshots, process, tracedIds, GetToolState(process, tracedIds));
                }
            }
        }

        return [.. snapshots
            .GroupBy(item => item.ProcessId)
            .Select(group => group.First())
            .OrderByDescending(item => item.IsMplrChild)
            .ThenByDescending(item => item.IsManaged)
            .ThenBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProcessId)];
    }

    public static void KillManagedChildProcesses()
    {
        ChildProcessTracerPeriodicTimer.Default.KillTracedProcesses();
    }

    private static void AddProcessSnapshot(List<ProcessDiagnosticSnapshot> snapshots, Process process, HashSet<int> tracedIds, string state)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            int? parentProcessId = process.Id == Environment.ProcessId ? null : Interop.GetParentProcessId(process.Id);
            bool isMplrChild = parentProcessId == Environment.ProcessId;
            bool isManaged = tracedIds.Contains(process.Id);
            snapshots.Add(new ProcessDiagnosticSnapshot(
                process.Id,
                process.ProcessName,
                parentProcessId,
                isMplrChild,
                isManaged,
                Math.Round(process.WorkingSet64 / 1024d / 1024d, 1),
                TryGetStartTime(process),
                state));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static string GetToolState(Process process, HashSet<int> tracedIds)
    {
        int? parentProcessId = Interop.GetParentProcessId(process.Id);
        if (tracedIds.Contains(process.Id))
        {
            return "MPLR 管理中";
        }

        if (parentProcessId == Environment.ProcessId)
        {
            return "MPLR 子进程，等待纳管";
        }

        return "外部进程";
    }

    private static string TryGetStartTime(Process process)
    {
        try
        {
            return process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }
}
