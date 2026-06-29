using Microsoft.Toolkit.Uwp.Notifications;

namespace MPLR.Core;

internal enum NotificationEventKind
{
    LiveStarted,
    RecordStarted,
    LiveStopped,
    RecordStopped,
    RecordError,
    MonitorError,
}

internal sealed class NotificationEvent
{
    public NotificationEventKind Kind { get; init; }

    public string RoomName { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

internal static class NotificationCenter
{
    private static readonly object SyncRoot = new();
    private static readonly List<NotificationEvent> Events = [];
    private static System.Threading.Timer? timer;
    private static bool started;

    public static void Start()
    {
        lock (SyncRoot)
        {
            if (started)
            {
                return;
            }

            started = true;
            timer = new System.Threading.Timer(_ => Flush(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    public static void Stop()
    {
        lock (SyncRoot)
        {
            timer?.Dispose();
            timer = null;
            started = false;
        }

        Flush(force: true);
    }

    public static void Publish(NotificationEventKind kind, string roomName, string detail = "")
    {
        if (!Configurations.IsToNotify.Get())
        {
            return;
        }

        lock (SyncRoot)
        {
            Events.Add(new NotificationEvent
            {
                Kind = kind,
                RoomName = roomName,
                Detail = detail,
            });
        }

        if (GetSummaryInterval() <= TimeSpan.Zero)
        {
            EnsureStarted();
        }
    }

    private static void EnsureStarted()
    {
        if (!started)
        {
            Start();
        }
    }

    private static void Flush(bool force = false)
    {
        NotificationEvent[] ready;
        lock (SyncRoot)
        {
            if (Events.Count == 0)
            {
                return;
            }

            if (force)
            {
                ready = [.. Events];
            }
            else
            {
                TimeSpan interval = GetSummaryInterval();
                TimeSpan delay = interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(800) : interval;
                DateTime threshold = DateTime.Now - delay;
                ready = Events.Where(item => item.CreatedAt <= threshold).ToArray();
            }
            if (ready.Length == 0)
            {
                return;
            }

            Events.RemoveAll(item => ready.Contains(item));
        }

        foreach (NotificationMessage message in BuildMessages(ready))
        {
            Send(message);
        }
    }

    private static TimeSpan GetSummaryInterval()
    {
        int minutes = Math.Clamp(Configurations.NotifySummaryIntervalMinutes.Get(), 0, 1440);
        return minutes <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
    }

    private static IEnumerable<NotificationMessage> BuildMessages(IReadOnlyList<NotificationEvent> events)
    {
        if (GetSummaryInterval() > TimeSpan.Zero)
        {
            yield return BuildSummary(events);
            yield break;
        }

        foreach (IGrouping<string, NotificationEvent> group in events.GroupBy(static item => item.RoomName))
        {
            HashSet<NotificationEventKind> kinds = group.Select(static item => item.Kind).ToHashSet();
            string detail = string.Join(Environment.NewLine, group.Select(static item => item.Detail).Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct());

            if (kinds.Contains(NotificationEventKind.MonitorError) || kinds.Contains(NotificationEventKind.RecordError))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 出现异常", detail);
            }
            else if (kinds.Contains(NotificationEventKind.LiveStarted) && kinds.Contains(NotificationEventKind.RecordStarted))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 已开播并开始录制", detail);
            }
            else if (kinds.Contains(NotificationEventKind.LiveStopped) && kinds.Contains(NotificationEventKind.RecordStopped))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 已下播并结束录制", detail);
            }
            else if (kinds.Contains(NotificationEventKind.RecordStarted))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 开始录制", detail);
            }
            else if (kinds.Contains(NotificationEventKind.LiveStarted))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 已开播", detail);
            }
            else if (kinds.Contains(NotificationEventKind.RecordStopped))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 录制已结束", detail);
            }
            else if (kinds.Contains(NotificationEventKind.LiveStopped))
            {
                yield return new NotificationMessage("通知", $"{group.Key} 已下播", detail);
            }
        }
    }

    private static NotificationMessage BuildSummary(IReadOnlyList<NotificationEvent> events)
    {
        string[] lines = events
            .GroupBy(static item => item.RoomName)
            .Select(group =>
            {
                string[] labels = group.Select(static item => GetKindText(item.Kind)).Distinct().ToArray();
                return $"{group.Key}：{string.Join("、", labels)}";
            })
            .ToArray();

        return new NotificationMessage("通知汇总", $"{events.Count} 条通知", string.Join(Environment.NewLine, lines));
    }

    private static string GetKindText(NotificationEventKind kind)
    {
        return kind switch
        {
            NotificationEventKind.LiveStarted => "开播",
            NotificationEventKind.RecordStarted => "开始录制",
            NotificationEventKind.LiveStopped => "下播",
            NotificationEventKind.RecordStopped => "录制结束",
            NotificationEventKind.RecordError => "录制异常",
            NotificationEventKind.MonitorError => "监控异常",
            _ => "通知",
        };
    }

    private static void Send(NotificationMessage message)
    {
        if (Configurations.IsToNotifyWithSystem.Get())
        {
            Notifier.AddNotice(message.Header, message.Title, message.Detail, ToastDuration.Short);
        }

        if (Configurations.IsToNotifyWithEmail.Get())
        {
            string smtpServer = Configurations.ToNotifyWithEmailSmtp.Get();
            string userName = Configurations.ToNotifyWithEmailUserName.Get();
            string password = Configurations.ToNotifyWithEmailPassword.Get();
            if (!string.IsNullOrWhiteSpace(smtpServer) && !string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
            {
                _ = Task.Run(() => Notifier.SendEmailHtml(smtpServer, userName, password, $"{message.Title} - {AppConfig.DisplayName}", $"<html><body><pre>{System.Net.WebUtility.HtmlEncode(message.Detail)}</pre></body></html>"));
            }
        }
    }

    private sealed record NotificationMessage(string Header, string Title, string Detail);
}
