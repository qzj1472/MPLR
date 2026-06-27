using CommunityToolkit.Mvvm.ComponentModel;
using MPLR.Core;
using MPLR.Extensions;
using MPLR.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using WindowsAPICodePack.Dialogs;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using CheckBox = System.Windows.Controls.CheckBox;

namespace MPLR.Views;

public partial class ScreenRecordListWindow : FluentWindow, INotifyPropertyChanged
{
    private readonly List<RecordedVideoItem> allVideos = [];
    private RecordedVideoItem? lastSelectedItem;
    private bool isMultiSelectMode;
    private bool isSortDescending;
    private string splitDurationValue = "30";
    private int splitDurationUnitIndex;
    private string mergeWarningText = string.Empty;

    public ObservableCollection<RecordedVideoItem> Videos { get; } = [];

    public bool IsMultiSelectMode
    {
        get => isMultiSelectMode;
        set
        {
            if (isMultiSelectMode == value)
            {
                return;
            }

            isMultiSelectMode = value;
            if (!value)
            {
                ClearSelection();
            }

            OnPropertyChanged(nameof(IsMultiSelectMode));
        }
    }

    public bool IsSortDescending
    {
        get => isSortDescending;
        set
        {
            if (isSortDescending == value)
            {
                return;
            }

            isSortDescending = value;
            OnPropertyChanged(nameof(IsSortDescending));
            OnPropertyChanged(nameof(SortDirectionText));
            ApplySort();
        }
    }

    public string SortDirectionText => IsSortDescending ? "倒序" : "正序";

    public int SelectedVideoCount => allVideos.Count(static video => video.IsSelected);

    public bool HasSelectedVideos => SelectedVideoCount > 0;

    public string SelectedVideoSummary => $"已选 {SelectedVideoCount} 个";

    public string SplitDurationValue
    {
        get => splitDurationValue;
        set
        {
            if (splitDurationValue == value)
            {
                return;
            }

            splitDurationValue = value;
            OnPropertyChanged(nameof(SplitDurationValue));
        }
    }

    public int SplitDurationUnitIndex
    {
        get => splitDurationUnitIndex;
        set
        {
            if (splitDurationUnitIndex == value)
            {
                return;
            }

            splitDurationUnitIndex = Math.Clamp(value, 0, 2);
            OnPropertyChanged(nameof(SplitDurationUnitIndex));
        }
    }

    public string MergeWarningText
    {
        get => mergeWarningText;
        set
        {
            if (mergeWarningText == value)
            {
                return;
            }

            mergeWarningText = value;
            OnPropertyChanged(nameof(MergeWarningText));
            OnPropertyChanged(nameof(HasMergeWarnings));
        }
    }

    public bool HasMergeWarnings => !string.IsNullOrWhiteSpace(MergeWarningText);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScreenRecordListWindow()
    {
        DataContext = this;
        WindowSizing.UseRelativeMainWindowSize(this, 1226d, 855d);
        InitializeComponent();
        SizeChanged += ScreenRecordListWindowSizeChanged;
        Loaded += async (_, _) => await ReloadVideosAsync();
    }

    private async Task ReloadVideosAsync()
    {
        allVideos.ForEach(item => item.PropertyChanged -= RecordedVideoPropertyChanged);
        allVideos.Clear();
        Videos.Clear();
        lastSelectedItem = null;

        string root = SaveFolderHelper.GetSaveFolder(Configurations.SaveFolder.Get());
        if (!Directory.Exists(root))
        {
            UpdateSelectedState();
            return;
        }

        FileInfo[] files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(static path => IsVideoFile(path))
            .Select(static path => new FileInfo(path))
            .ToArray();

        foreach (FileInfo file in files)
        {
            RecordedVideoItem item = CreateItem(file, root);
            item.PropertyChanged += RecordedVideoPropertyChanged;
            allVideos.Add(item);
        }

        ApplySort();
        UpdateSelectedState();
        await Task.WhenAll(allVideos.Select(EnrichItemAsync));
    }

    private static RecordedVideoItem CreateItem(FileInfo file, string root)
    {
        VideoRecordingMetadata metadata = LoadMetadata(file);
        string nickName = string.IsNullOrWhiteSpace(metadata.NickName) ? GuessNickName(file, root) : metadata.NickName;
        DateTime recordedAt = metadata.RecordedAt > DateTime.MinValue ? metadata.RecordedAt : file.LastWriteTime;

        return new RecordedVideoItem
        {
            FilePath = file.FullName,
            FileName = file.Name,
            DirectoryPath = file.DirectoryName ?? root,
            NickName = string.IsNullOrWhiteSpace(nickName) ? "未知主播" : nickName,
            Resolution = string.IsNullOrWhiteSpace(metadata.Resolution) ? "分辨率未知" : metadata.Resolution,
            Bitrate = string.IsNullOrWhiteSpace(metadata.Bitrate) ? "码率未知" : metadata.Bitrate,
            Title = string.IsNullOrWhiteSpace(metadata.Title) ? "直播间标题未知" : metadata.Title,
            Platform = metadata.Platform,
            CoverPath = metadata.CoverPath,
            RecordedAt = recordedAt,
            LastWriteTime = file.LastWriteTime,
            SortName = BuildSortName(file),
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
        yield return GetDirectMetadataPath(file);

        if (TryGetSegmentBaseStem(file, out string baseStem))
        {
            yield return GetSharedSegmentMetadataPath(file, baseStem);
        }
    }

    private static string GetDirectMetadataPath(FileInfo file)
    {
        string directory = file.DirectoryName ?? string.Empty;
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file.Name)}.mplr.json");
    }

    private static string GetSharedSegmentMetadataPath(FileInfo file, string baseStem)
    {
        string directory = file.DirectoryName ?? string.Empty;
        return Path.Combine(directory, $"{baseStem}.mplr.json");
    }

    private static bool TryGetSegmentBaseStem(FileInfo file, out string baseStem)
    {
        string stem = Path.GetFileNameWithoutExtension(file.Name);
        if (stem.Length > 4 && stem[^4] == '_' && int.TryParse(stem[^3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            baseStem = stem[..^4];
            return true;
        }

        baseStem = string.Empty;
        return false;
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
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);

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

    private static string BuildSortName(FileInfo file)
    {
        return Path.GetFileNameWithoutExtension(file.Name).Trim().ToUpperInvariant();
    }

    private static string SanitizeFolderName(string value)
    {
        string name = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        name = name.TrimEnd('.');
        return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
    }

    private static string BuildClassifiedFolder(string root, VideoRecordingMetadata metadata, FileInfo file, string sourceRoot)
    {
        string nickName = string.IsNullOrWhiteSpace(metadata.NickName) ? GuessNickName(file, sourceRoot) : metadata.NickName;
        string author = SanitizeFolderName(nickName);
        string platform = string.IsNullOrWhiteSpace(metadata.Platform) ? "Unknown" : SanitizeFolderName(metadata.Platform);
        DateTime time = metadata.RecordedAt > DateTime.MinValue ? metadata.RecordedAt : file.LastWriteTime;
        string month = time.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        return Configurations.SaveFolderPathLevel.Get() switch
        {
            1 => Path.Combine(root, platform, author, month),
            _ => Path.Combine(root, author, month),
        };
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 1; i < 10000; i++)
        {
            string candidate = Path.Combine(directory, $"{name}_{i:000}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name}_{Guid.NewGuid():N}{extension}");
    }

    private static void CopyMetadataForTarget(FileInfo source, string targetVideoPath, bool move)
    {
        string directMetadataPath = GetDirectMetadataPath(source);
        if (File.Exists(directMetadataPath))
        {
            CopyMetadataFileForTarget(directMetadataPath, targetVideoPath, move);
            return;
        }

        if (TryGetSegmentBaseStem(source, out string baseStem))
        {
            string sharedMetadataPath = GetSharedSegmentMetadataPath(source, baseStem);
            if (File.Exists(sharedMetadataPath))
            {
                CopyMetadataFileForTarget(sharedMetadataPath, targetVideoPath, false);
            }
        }
    }

    private static void CopyMetadataFileForTarget(string metadataPath, string targetVideoPath, bool move)
    {
        string targetMetadataPath = Path.Combine(Path.GetDirectoryName(targetVideoPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(targetVideoPath)}.mplr.json");
        targetMetadataPath = GetUniquePath(targetMetadataPath);

        if (move)
        {
            File.Move(metadataPath, targetMetadataPath);
            return;
        }

        File.Copy(metadataPath, targetMetadataPath);
    }

    private static bool IsSameOrAncestorDirectory(string parent, string child)
    {
        string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedChild = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedChild.Equals(normalizedParent, StringComparison.OrdinalIgnoreCase) ||
            normalizedChild.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedChild.StartsWith(normalizedParent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldDeleteSharedSegmentMetadata(FileInfo file, ISet<string> selectedPaths)
    {
        if (!TryGetSegmentBaseStem(file, out string baseStem) || string.IsNullOrWhiteSpace(file.DirectoryName))
        {
            return false;
        }

        return Directory.EnumerateFiles(file.DirectoryName, $"{baseStem}_*.*", SearchOption.TopDirectoryOnly)
            .Where(IsVideoFile)
            .All(path => selectedPaths.Contains(Path.GetFullPath(path)));
    }

    private static IReadOnlyList<RecordedVideoItem> SnapshotSelected(IEnumerable<RecordedVideoItem> source)
    {
        return source.Where(static item => item.IsSelected && File.Exists(item.FilePath)).ToArray();
    }

    private static string? SelectFolder(string title)
    {
        using CommonOpenFileDialog dialog = new()
        {
            IsFolderPicker = true,
            Title = title,
        };

        return dialog.ShowDialog() == CommonFileDialogResult.Ok ? dialog.FileName : null;
    }

    private void ApplySort()
    {
        IEnumerable<RecordedVideoItem> sorted = IsSortDescending
            ? allVideos.OrderByDescending(static item => item.SortName, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(static item => item.RecordedAt)
                .ThenByDescending(static item => item.FileName, StringComparer.CurrentCultureIgnoreCase)
            : allVideos.OrderBy(static item => item.SortName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static item => item.RecordedAt)
                .ThenBy(static item => item.FileName, StringComparer.CurrentCultureIgnoreCase);

        Videos.Clear();
        foreach (RecordedVideoItem item in sorted)
        {
            Videos.Add(item);
        }
    }

    private void ClearSelection()
    {
        foreach (RecordedVideoItem item in allVideos)
        {
            item.IsSelected = false;
        }

        lastSelectedItem = null;
        UpdateSelectedState();
    }

    private void UpdateSelectedState()
    {
        OnPropertyChanged(nameof(SelectedVideoCount));
        OnPropertyChanged(nameof(HasSelectedVideos));
        OnPropertyChanged(nameof(SelectedVideoSummary));
    }

    private void RecordedVideoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecordedVideoItem.IsSelected))
        {
            UpdateSelectedState();
        }
    }

    private void SelectRange(RecordedVideoItem item, bool selected)
    {
        if (lastSelectedItem == null)
        {
            lastSelectedItem = item;
            return;
        }

        int start = Videos.IndexOf(lastSelectedItem);
        int end = Videos.IndexOf(item);
        if (start < 0 || end < 0)
        {
            lastSelectedItem = item;
            return;
        }

        if (start > end)
        {
            (start, end) = (end, start);
        }

        for (int i = start; i <= end; i++)
        {
            Videos[i].IsSelected = selected;
        }

        lastSelectedItem = item;
    }

    private async Task MoveOrCopySelectedAsync(bool move)
    {
        IReadOnlyList<RecordedVideoItem> selected = SnapshotSelected(allVideos);
        if (selected.Count == 0)
        {
            return;
        }

        string? targetFolder = SelectFolder(move ? "选择移动到的文件夹" : "选择复制到的文件夹");
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            return;
        }

        Directory.CreateDirectory(targetFolder);
        int done = 0;
        foreach (RecordedVideoItem item in selected)
        {
            try
            {
                FileInfo source = new(item.FilePath);
                string target = GetUniquePath(Path.Combine(targetFolder, source.Name));

                if (move)
                {
                    File.Move(source.FullName, target);
                    CopyMetadataForTarget(source, target, true);
                }
                else
                {
                    File.Copy(source.FullName, target);
                    CopyMetadataForTarget(source, target, false);
                }

                done++;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        Toast.Success(move ? $"已移动 {done} 个视频" : $"已复制 {done} 个视频");
        await ReloadVideosAsync();
    }

    private async Task ImportFolderAsync()
    {
        string? sourceFolder = SelectFolder("选择要导入的视频文件夹");
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
        {
            return;
        }

        string root = SaveFolderHelper.GetSaveFolder(Configurations.SaveFolder.Get());
        if (IsSameOrAncestorDirectory(sourceFolder, root) || IsSameOrAncestorDirectory(root, sourceFolder))
        {
            Toast.Error("不能从当前保存目录、其上级目录或子目录导入视频");
            return;
        }

        int count = 0;

        foreach (string path in Directory.EnumerateFiles(sourceFolder, "*.*", SearchOption.AllDirectories).Where(IsVideoFile))
        {
            try
            {
                FileInfo file = new(path);
                VideoRecordingMetadata metadata = LoadMetadata(file);
                string targetFolder = BuildClassifiedFolder(root, metadata, file, sourceFolder);
                Directory.CreateDirectory(targetFolder);
                string targetPath = GetUniquePath(Path.Combine(targetFolder, file.Name));

                if (string.Equals(file.FullName, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file.FullName, targetPath);
                CopyMetadataForTarget(file, targetPath, false);
                count++;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        Toast.Success($"已导入 {count} 个视频");
        await ReloadVideosAsync();
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        await ReloadVideosAsync();
    }

    private void ToggleSortClick(object sender, RoutedEventArgs e)
    {
        IsSortDescending = !IsSortDescending;
    }

    private void ToggleMultiSelectClick(object sender, RoutedEventArgs e)
    {
        IsMultiSelectMode = !IsMultiSelectMode;
    }

    private void CancelMultiSelectClick(object sender, RoutedEventArgs e)
    {
        IsMultiSelectMode = false;
    }

    private async void DeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<RecordedVideoItem> selected = SnapshotSelected(allVideos);
        if (selected.Count == 0)
        {
            return;
        }

        System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show($"确定删除选中的 {selected.Count} 个视频吗？", "删除视频", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        int deleted = 0;
        HashSet<string> selectedPaths = selected
            .Select(static item => Path.GetFullPath(item.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (RecordedVideoItem item in selected)
        {
            try
            {
                FileInfo file = new(item.FilePath);
                string directMetadataPath = GetDirectMetadataPath(file);
                if (File.Exists(directMetadataPath))
                {
                    File.Delete(directMetadataPath);
                }

                if (ShouldDeleteSharedSegmentMetadata(file, selectedPaths))
                {
                    string sharedMetadataPath = GetSharedSegmentMetadataPath(file, Path.GetFileNameWithoutExtension(file.Name)[..^4]);
                    if (File.Exists(sharedMetadataPath))
                    {
                        File.Delete(sharedMetadataPath);
                    }
                }

                File.Delete(item.FilePath);
                deleted++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        Toast.Success($"已删除 {deleted} 个视频");
        await ReloadVideosAsync();
    }

    private async void MoveSelectedClick(object sender, RoutedEventArgs e)
    {
        await MoveOrCopySelectedAsync(true);
    }

    private async void CopySelectedClick(object sender, RoutedEventArgs e)
    {
        await MoveOrCopySelectedAsync(false);
    }

    private async void ImportFolderClick(object sender, RoutedEventArgs e)
    {
        await ImportFolderAsync();
    }

    private void OpenSplitVideoFlyoutClick(object sender, RoutedEventArgs e)
    {
        SplitDurationValue = "30";
        SplitDurationUnitIndex = 0;
        OpenCenteredFlyout(SplitVideoFlyout);
    }

    private void OpenMergeSelectedClick(object sender, RoutedEventArgs e)
    {
        MergeWarningText = BuildMergeWarningText(SnapshotSelected(allVideos));
        OpenCenteredFlyout(MergeVideoFlyout);
    }

    private async void ConfirmMergeSelectedClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<RecordedVideoItem> selected = SnapshotSelected(allVideos);
        MergeWarningText = BuildMergeWarningText(selected);

        if (!string.IsNullOrWhiteSpace(MergeWarningText))
        {
            Toast.Warning("选中的视频不符合合并条件");
            return;
        }

        bool ok = await MergeSelectedVideosAsync(selected);
        if (ok)
        {
            CloseVideoListModals();
            Toast.Success("合并完成");
            await ReloadVideosAsync();
        }
        else
        {
            Toast.Error("合并失败");
        }
    }

    private void CloseVideoListModalClick(object sender, RoutedEventArgs e)
    {
        CloseVideoListModals();
    }

    private void VideoListModalOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        CloseVideoListModals();
        e.Handled = true;
    }

    private void OpenCenteredFlyout(FrameworkElement flyout)
    {
        CloseVideoListModals(flyout);
        flyout.Visibility = Visibility.Visible;
        UpdateVideoListModalOverlay();
        Dispatcher.BeginInvoke(() => CenterVisibleFlyout(flyout), DispatcherPriority.Loaded);
    }

    private void CenterVisibleFlyout(FrameworkElement flyout)
    {
        if (flyout.Visibility != Visibility.Visible)
        {
            return;
        }

        flyout.UpdateLayout();
        VideoListFlyoutLayer.UpdateLayout();

        double flyoutWidth = Math.Max(flyout.ActualWidth, flyout.DesiredSize.Width);
        double flyoutHeight = Math.Max(flyout.ActualHeight, flyout.DesiredSize.Height);
        double layerWidth = VideoListFlyoutLayer.ActualWidth > 1 ? VideoListFlyoutLayer.ActualWidth : Math.Max(ActualWidth, 1);
        double layerHeight = VideoListFlyoutLayer.ActualHeight > 1 ? VideoListFlyoutLayer.ActualHeight : Math.Max(ActualHeight, 1);
        double left = Math.Clamp((layerWidth - flyoutWidth) / 2d, 0, Math.Max(0, layerWidth - flyoutWidth));
        double top = Math.Clamp((layerHeight - flyoutHeight) / 2d, 0, Math.Max(0, layerHeight - flyoutHeight));

        Canvas.SetLeft(flyout, left);
        Canvas.SetTop(flyout, top);
    }

    private void CloseVideoListModals(FrameworkElement? except = null)
    {
        foreach (FrameworkElement flyout in new[] { SplitVideoFlyout, MergeVideoFlyout })
        {
            if (!ReferenceEquals(flyout, except))
            {
                flyout.Visibility = Visibility.Collapsed;
            }
        }

        UpdateVideoListModalOverlay();
    }

    private void UpdateVideoListModalOverlay()
    {
        bool visible = SplitVideoFlyout.Visibility == Visibility.Visible || MergeVideoFlyout.Visibility == Visibility.Visible;
        VideoListModalOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScreenRecordListWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (FrameworkElement flyout in new[] { SplitVideoFlyout, MergeVideoFlyout })
            {
                if (flyout.Visibility == Visibility.Visible)
                {
                    CenterVisibleFlyout(flyout);
                }
            }
        }, DispatcherPriority.Loaded);
    }

    private static string BuildMergeWarningText(IReadOnlyList<RecordedVideoItem> selected)
    {
        List<string> reasons = [];

        if (selected.Count < 2)
        {
            reasons.Add("至少需要选择两个视频");
        }

        if (selected.Select(static item => item.NickName).Distinct(StringComparer.CurrentCultureIgnoreCase).Count() > 1)
        {
            reasons.Add("不属于同一个直播间");
        }

        if (selected.Any(static item => !TryGetSegmentBaseStem(new FileInfo(item.FilePath), out _)))
        {
            reasons.Add("包含非分割操作产生的原生独立视频");
        }

        if (!IsContinuousSegmentSelection(selected))
        {
            reasons.Add("不属于同一连续时间段");
        }

        return reasons.Count == 0 ? string.Empty : "PS：" + string.Join("；", reasons.Distinct());
    }

    private static bool IsContinuousSegmentSelection(IReadOnlyList<RecordedVideoItem> selected)
    {
        if (selected.Count < 2)
        {
            return false;
        }

        List<(string BaseStem, int Index)> segments = [];

        foreach (RecordedVideoItem item in selected)
        {
            FileInfo file = new(item.FilePath);
            string stem = Path.GetFileNameWithoutExtension(file.Name);
            if (!TryGetSegmentBaseStem(file, out string baseStem) ||
                !int.TryParse(stem[^3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return false;
            }

            segments.Add((baseStem, index));
        }

        if (segments.Select(static item => item.BaseStem).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
        {
            return false;
        }

        int[] indexes = segments.Select(static item => item.Index).Order().ToArray();
        for (int i = 1; i < indexes.Length; i++)
        {
            if (indexes[i] != indexes[i - 1] + 1)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> MergeSelectedVideosAsync(IReadOnlyList<RecordedVideoItem> selected)
    {
        string? ffmpegPath = SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            return false;
        }

        RecordedVideoItem[] ordered = selected
            .OrderBy(static item => GetSegmentIndex(item.FilePath))
            .ToArray();

        FileInfo first = new(ordered[0].FilePath);
        if (!TryGetSegmentBaseStem(first, out string baseStem) || string.IsNullOrWhiteSpace(first.DirectoryName))
        {
            return false;
        }

        string extension = first.Extension;
        string target = GetUniquePath(Path.Combine(first.DirectoryName, $"{baseStem}_merged{extension}"));
        string listPath = Path.Combine(Path.GetTempPath(), $"mplr_merge_{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllLinesAsync(listPath, ordered.Select(static item => $"file '{EscapeConcatPath(item.FilePath)}'"), Encoding.UTF8);

            ProcessStartInfo startInfo = new()
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };

            foreach (string argument in new[] { "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-c", "copy", target })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            process.Start();
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            _ = await outputTask;
            _ = await errorTask;

            return process.ExitCode == 0 && File.Exists(target);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(listPath))
                {
                    File.Delete(listPath);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }

    private static int GetSegmentIndex(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        return stem.Length > 3 && int.TryParse(stem[^3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
            ? index
            : 0;
    }

    private static string EscapeConcatPath(string path)
    {
        return path.Replace("\\", "/", StringComparison.Ordinal).Replace("'", "'\\''", StringComparison.Ordinal);
    }

    private void SelectionCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: RecordedVideoItem item })
        {
            return;
        }

        bool selected = item.IsSelected;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            SelectRange(item, selected);
        }
        else
        {
            lastSelectedItem = item;
        }
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

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    private string platform = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private string thumbnailPath = string.Empty;

    [ObservableProperty]
    private string coverPath = string.Empty;

    [ObservableProperty]
    private DateTime recordedAt;

    [ObservableProperty]
    private DateTime lastWriteTime;

    [ObservableProperty]
    private string sortName = string.Empty;

    [ObservableProperty]
    private bool canTranscode;

    [ObservableProperty]
    private bool isSelected;

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath);
}
