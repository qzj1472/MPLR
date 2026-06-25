using CommunityToolkit.Mvvm.ComponentModel;
using MPLR.Core;
using MPLR.Extensions;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

namespace MPLR.Views;

public partial class ScreenRecordListWindow : FluentWindow
{
    public ObservableCollection<RecordedVideoItem> Videos { get; } = [];

    public ScreenRecordListWindow()
    {
        DataContext = this;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadVideosAsync();
    }

    private async Task ReloadVideosAsync()
    {
        Videos.Clear();

        string root = SaveFolderHelper.GetSaveFolder(Configurations.SaveFolder.Get());
        if (!Directory.Exists(root))
        {
            return;
        }

        FileInfo[] files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(static path => IsVideoFile(path))
            .Select(static path => new FileInfo(path))
            .OrderByDescending(static file => file.LastWriteTime)
            .ToArray();

        foreach (FileInfo file in files)
        {
            Videos.Add(CreateItem(file, root));
        }

        await Task.WhenAll(Videos.Select(EnrichItemAsync));
    }

    private static RecordedVideoItem CreateItem(FileInfo file, string root)
    {
        VideoRecordingMetadata metadata = LoadMetadata(file);
        string nickName = string.IsNullOrWhiteSpace(metadata.NickName) ? GuessNickName(file, root) : metadata.NickName;

        return new RecordedVideoItem
        {
            FilePath = file.FullName,
            FileName = file.Name,
            DirectoryPath = file.DirectoryName ?? root,
            NickName = string.IsNullOrWhiteSpace(nickName) ? "未知主播" : nickName,
            Resolution = string.IsNullOrWhiteSpace(metadata.Resolution) ? "分辨率未知" : metadata.Resolution,
            Bitrate = string.IsNullOrWhiteSpace(metadata.Bitrate) ? "码率未知" : metadata.Bitrate,
            Title = string.IsNullOrWhiteSpace(metadata.Title) ? "直播间标题未知" : metadata.Title,
            CoverPath = metadata.CoverPath,
            CanTranscode = file.Extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".flv", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static async Task EnrichItemAsync(RecordedVideoItem item)
    {
        if (File.Exists(item.CoverPath))
        {
            item.ThumbnailPath = item.CoverPath;
        }
        else
        {
            item.ThumbnailPath = await ExtractThumbnailAsync(item.FilePath);
        }

        MediaProbeResult probe = await MediaProbe.ProbeAsync(item.FilePath);
        if (!string.IsNullOrWhiteSpace(probe.Resolution))
        {
            item.Resolution = probe.Resolution;
        }

        if (!string.IsNullOrWhiteSpace(probe.Bitrate))
        {
            item.Bitrate = probe.Bitrate;
        }
    }

    private static VideoRecordingMetadata LoadMetadata(FileInfo file)
    {
        foreach (string path in GetMetadataCandidates(file))
        {
            try
            {
                if (File.Exists(path))
                {
                    return JsonSerializer.Deserialize<VideoRecordingMetadata>(File.ReadAllText(path)) ?? new VideoRecordingMetadata();
                }
            }
            catch
            {
            }
        }

        return new VideoRecordingMetadata();
    }

    private static IEnumerable<string> GetMetadataCandidates(FileInfo file)
    {
        string directory = file.DirectoryName ?? string.Empty;
        yield return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file.Name)}.mplr.json");

        string stem = Path.GetFileNameWithoutExtension(file.Name);
        if (stem.Length > 4 && stem[^4] == '_' && int.TryParse(stem[^3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            yield return Path.Combine(directory, $"{stem[..^4]}.mplr.json");
        }
    }

    private static string GuessNickName(FileInfo file, string root)
    {
        DirectoryInfo? directory = file.Directory;
        if (directory == null)
        {
            return string.Empty;
        }

        if (DateTime.TryParseExact(directory.Name, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
            directory.Parent != null)
        {
            return directory.Parent.Name;
        }

        string relative = Path.GetRelativePath(root, directory.FullName);
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length > 0 ? parts[^1] : string.Empty;
    }

    private static async Task<string> ExtractThumbnailAsync(string filePath)
    {
        string? ffmpegPath = SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            return string.Empty;
        }

        string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "video_thumbnails");
        Directory.CreateDirectory(cacheDir);
        string imagePath = Path.Combine(cacheDir, $"{ToStableHash(filePath)}.jpg");

        if (File.Exists(imagePath))
        {
            return imagePath;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add("00:00:01");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-vf");
            startInfo.ArgumentList.Add("scale=320:-1");
            startInfo.ArgumentList.Add(imagePath);

            using Process process = new() { StartInfo = startInfo };
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            if (!await Task.Run(() => process.WaitForExit(12000)))
            {
                process.Kill(entireProcessTree: true);
                return string.Empty;
            }

            _ = await outputTask;
            _ = await errorTask;

            return File.Exists(imagePath) ? imagePath : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ToStableHash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    private static bool IsVideoFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".flv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private void OpenVideoClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecordedVideoItem item } && File.Exists(item.FilePath))
        {
            OpenPath(item.FilePath);
        }
    }

    private void OpenDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecordedVideoItem item } && Directory.Exists(item.DirectoryPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { "/select,", item.FilePath },
                UseShellExecute = true,
            });
        }
    }

    private async void TranscodeVideoClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecordedVideoItem item } || !File.Exists(item.FilePath))
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "转码",
            Content = $"选择 {item.FileName} 的目标格式",
            PrimaryButtonText = "MP4",
            SecondaryButtonText = "MKV",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        string target = result switch
        {
            ContentDialogResult.Primary => ".mp4",
            ContentDialogResult.Secondary => ".mkv",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        bool ok = await new Converter().ExecuteAsync(item.FilePath, target);
        if (ok)
        {
            Toast.Success("转码完成");
            await ReloadVideosAsync();
        }
        else
        {
            Toast.Error("转码失败");
        }
    }
}

public sealed partial class RecordedVideoItem : ObservableObject
{
    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private string directoryPath = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private string nickName = string.Empty;

    [ObservableProperty]
    private string resolution = string.Empty;

    [ObservableProperty]
    private string bitrate = string.Empty;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private string thumbnailPath = string.Empty;

    [ObservableProperty]
    private string coverPath = string.Empty;

    [ObservableProperty]
    private bool canTranscode;

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath);
}
