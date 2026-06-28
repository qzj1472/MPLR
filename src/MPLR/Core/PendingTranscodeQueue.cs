using System.Text.Json;

namespace MPLR.Core;

internal sealed class PendingTranscodeTask
{
    public string SourceFile { get; set; } = string.Empty;

    public string TargetFormat { get; set; } = string.Empty;

    public bool RemoveSource { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

internal static class PendingTranscodeQueue
{
    private static readonly object SyncRoot = new();

    public static void Enqueue(IEnumerable<string> sourceFiles, string targetFormat, bool removeSource)
    {
        PendingTranscodeTask[] tasks = sourceFiles
            .Where(static file => !string.IsNullOrWhiteSpace(file) && File.Exists(file))
            .Select(file => new PendingTranscodeTask
            {
                SourceFile = file,
                TargetFormat = targetFormat,
                RemoveSource = removeSource,
            })
            .ToArray();

        if (tasks.Length == 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            List<PendingTranscodeTask> allTasks = LoadUnsafe();
            foreach (PendingTranscodeTask task in tasks)
            {
                if (!allTasks.Any(item => string.Equals(item.SourceFile, task.SourceFile, StringComparison.OrdinalIgnoreCase) &&
                                          string.Equals(item.TargetFormat, task.TargetFormat, StringComparison.OrdinalIgnoreCase)))
                {
                    allTasks.Add(task);
                }
            }

            SaveUnsafe(allTasks);
        }
    }

    public static async Task ProcessAsync(CancellationToken token = default)
    {
        PendingTranscodeTask[] snapshot;
        lock (SyncRoot)
        {
            snapshot = [.. LoadUnsafe()];
        }

        if (snapshot.Length == 0)
        {
            return;
        }

        List<PendingTranscodeTask> completed = [];
        foreach (PendingTranscodeTask task in snapshot)
        {
            token.ThrowIfCancellationRequested();

            if (!File.Exists(task.SourceFile))
            {
                completed.Add(task);
                continue;
            }

            bool ok = await new Converter().ExecuteAsync(task.SourceFile, task.TargetFormat);
            if (ok)
            {
                if (task.RemoveSource)
                {
                    TryDelete(task.SourceFile);
                }

                completed.Add(task);
            }
        }

        lock (SyncRoot)
        {
            List<PendingTranscodeTask> current = LoadUnsafe();
            current.RemoveAll(item => completed.Any(done => IsSameTask(item, done)));
            SaveUnsafe(current);
        }
    }

    private static bool IsSameTask(PendingTranscodeTask left, PendingTranscodeTask right)
    {
        return string.Equals(left.SourceFile, right.SourceFile, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.TargetFormat, right.TargetFormat, StringComparison.OrdinalIgnoreCase);
    }

    private static List<PendingTranscodeTask> LoadUnsafe()
    {
        try
        {
            if (!File.Exists(AppPaths.PendingTranscodeQueuePath))
            {
                return [];
            }

            string json = File.ReadAllText(AppPaths.PendingTranscodeQueuePath);
            return JsonSerializer.Deserialize<List<PendingTranscodeTask>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveUnsafe(List<PendingTranscodeTask> tasks)
    {
        Directory.CreateDirectory(AppPaths.CacheDirectory);
        string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.PendingTranscodeQueuePath, json);
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch
        {
        }
    }
}
