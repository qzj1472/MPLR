using Microsoft.Toolkit.Uwp.Notifications;
using CommunityToolkit.Mvvm.Messaging;
using Fischless.Configuration;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using MPLR.Core;
using MPLR.Extensions;
using MPLR.Threading;
using MPLR.ViewModels;
using Vanara.PInvoke;
using Windows.Storage;
using Windows.System;
using WindowsAPICodePack.Dialogs;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace MPLR.Views;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }

    public static readonly DependencyProperty RoomCardItemWidthProperty = DependencyProperty.Register(nameof(RoomCardItemWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(196d));

    public static readonly DependencyProperty RoomCardItemHeightProperty = DependencyProperty.Register(nameof(RoomCardItemHeight), typeof(double), typeof(MainWindow), new PropertyMetadata(132d));

    public static readonly DependencyProperty RoomCardPanelWidthProperty = DependencyProperty.Register(nameof(RoomCardPanelWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(196d));

    public static readonly DependencyProperty RoomCardWidthProperty = DependencyProperty.Register(nameof(RoomCardWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(184d));

    public static readonly DependencyProperty RoomCardHeightProperty = DependencyProperty.Register(nameof(RoomCardHeight), typeof(double), typeof(MainWindow), new PropertyMetadata(122d));

    public static readonly DependencyProperty RoomCardPaddingProperty = DependencyProperty.Register(nameof(RoomCardPadding), typeof(Thickness), typeof(MainWindow), new PropertyMetadata(new Thickness(8)));

    public static readonly DependencyProperty RoomCardMarginProperty = DependencyProperty.Register(nameof(RoomCardMargin), typeof(Thickness), typeof(MainWindow), new PropertyMetadata(new Thickness(4)));

    public static readonly DependencyProperty RoomCardAvatarSizeProperty = DependencyProperty.Register(nameof(RoomCardAvatarSize), typeof(double), typeof(MainWindow), new PropertyMetadata(32d));

    public static readonly DependencyProperty RoomCardAvatarContainerSizeProperty = DependencyProperty.Register(nameof(RoomCardAvatarContainerSize), typeof(double), typeof(MainWindow), new PropertyMetadata(36d));

    public static readonly DependencyProperty RoomCardAvatarIconSizeProperty = DependencyProperty.Register(nameof(RoomCardAvatarIconSize), typeof(double), typeof(MainWindow), new PropertyMetadata(18d));

    public static readonly DependencyProperty RoomCardHeaderColumnWidthProperty = DependencyProperty.Register(nameof(RoomCardHeaderColumnWidth), typeof(GridLength), typeof(MainWindow), new PropertyMetadata(new GridLength(38)));

    public static readonly DependencyProperty RoomCardAvatarMarginProperty = DependencyProperty.Register(nameof(RoomCardAvatarMargin), typeof(Thickness), typeof(MainWindow), new PropertyMetadata(new Thickness(3, 3, 10, 0)));

    public static readonly DependencyProperty RoomCardNameFontSizeProperty = DependencyProperty.Register(nameof(RoomCardNameFontSize), typeof(double), typeof(MainWindow), new PropertyMetadata(13d));

    public static readonly DependencyProperty RoomCardPlatformFontSizeProperty = DependencyProperty.Register(nameof(RoomCardPlatformFontSize), typeof(double), typeof(MainWindow), new PropertyMetadata(11d));

    public static readonly DependencyProperty RoomCardTitleFontSizeProperty = DependencyProperty.Register(nameof(RoomCardTitleFontSize), typeof(double), typeof(MainWindow), new PropertyMetadata(11d));

    public static readonly DependencyProperty RoomCardTitleLineHeightProperty = DependencyProperty.Register(nameof(RoomCardTitleLineHeight), typeof(double), typeof(MainWindow), new PropertyMetadata(15d));

    public static readonly DependencyProperty RoomCardTitleMaxHeightProperty = DependencyProperty.Register(nameof(RoomCardTitleMaxHeight), typeof(double), typeof(MainWindow), new PropertyMetadata(30d));

    public static readonly DependencyProperty RoomCardTitleVisibilityProperty = DependencyProperty.Register(nameof(RoomCardTitleVisibility), typeof(Visibility), typeof(MainWindow), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty RoomCardChipFontSizeProperty = DependencyProperty.Register(nameof(RoomCardChipFontSize), typeof(double), typeof(MainWindow), new PropertyMetadata(11d));

    public static readonly DependencyProperty RoomCardChipPaddingProperty = DependencyProperty.Register(nameof(RoomCardChipPadding), typeof(Thickness), typeof(MainWindow), new PropertyMetadata(new Thickness(4, 1, 4, 1)));

    public static readonly DependencyProperty RoomCardChipMinHeightProperty = DependencyProperty.Register(nameof(RoomCardChipMinHeight), typeof(double), typeof(MainWindow), new PropertyMetadata(20d));

    public static readonly DependencyProperty CanUseLargeRoomCardsProperty = DependencyProperty.Register(nameof(CanUseLargeRoomCards), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty RoutineIntervalSecondsTextProperty = DependencyProperty.Register(nameof(RoutineIntervalSecondsText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RoutineIntervalMinutesTextProperty = DependencyProperty.Register(nameof(RoutineIntervalMinutesText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SegmentTimeSecondsTextProperty = DependencyProperty.Register(nameof(SegmentTimeSecondsText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SegmentTimeMinutesTextProperty = DependencyProperty.Register(nameof(SegmentTimeMinutesText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SegmentTimeHoursTextProperty = DependencyProperty.Register(nameof(SegmentTimeHoursText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SegmentTimeMegabytesTextProperty = DependencyProperty.Register(nameof(SegmentTimeMegabytesText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SegmentTimeGigabytesTextProperty = DependencyProperty.Register(nameof(SegmentTimeGigabytesText), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LocalRecordFormatIndexProperty = DependencyProperty.Register(nameof(LocalRecordFormatIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalStreamQualityIndexProperty = DependencyProperty.Register(nameof(LocalStreamQualityIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalIsToRecordProperty = DependencyProperty.Register(nameof(LocalIsToRecord), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty LocalIsToMonitorProperty = DependencyProperty.Register(nameof(LocalIsToMonitor), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty LocalIsRemoveTsProperty = DependencyProperty.Register(nameof(LocalIsRemoveTs), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty LocalIsToSegmentProperty = DependencyProperty.Register(nameof(LocalIsToSegment), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty LocalSegmentTimeValueProperty = DependencyProperty.Register(nameof(LocalSegmentTimeValue), typeof(double), typeof(MainWindow), new PropertyMetadata(30d));

    public static readonly DependencyProperty LocalSegmentTimeUnitIndexProperty = DependencyProperty.Register(nameof(LocalSegmentTimeUnitIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalRoutineIntervalValueProperty = DependencyProperty.Register(nameof(LocalRoutineIntervalValue), typeof(double), typeof(MainWindow), new PropertyMetadata(3d));

    public static readonly DependencyProperty LocalRoutineIntervalUnitIndexProperty = DependencyProperty.Register(nameof(LocalRoutineIntervalUnitIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalRoutineScheduleModeIndexProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleModeIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0, OnLocalRoutineScheduleModeIndexChanged));

    public static readonly DependencyProperty IsLocalRoutineScheduleCustomProperty = DependencyProperty.Register(nameof(IsLocalRoutineScheduleCustom), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLocalRoutineScheduleAlwaysProperty = DependencyProperty.Register(nameof(IsLocalRoutineScheduleAlways), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleMondayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleMonday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleTuesdayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleTuesday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleWednesdayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleWednesday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleThursdayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleThursday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleFridayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleFriday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleSaturdayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleSaturday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleSundayProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleSunday), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty LocalRoutineScheduleStartHourProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleStartHour), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalRoutineScheduleStartMinuteProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleStartMinute), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalRoutineScheduleEndHourProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleEndHour), typeof(int), typeof(MainWindow), new PropertyMetadata(23));

    public static readonly DependencyProperty LocalRoutineScheduleEndMinuteProperty = DependencyProperty.Register(nameof(LocalRoutineScheduleEndMinute), typeof(int), typeof(MainWindow), new PropertyMetadata(59));

    public static readonly DependencyProperty LocalSaveFolderProperty = DependencyProperty.Register(nameof(LocalSaveFolder), typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LocalSaveFolderPathLevelIndexProperty = DependencyProperty.Register(nameof(LocalSaveFolderPathLevelIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    public static readonly DependencyProperty LocalSaveFileNameRuleIndexProperty = DependencyProperty.Register(nameof(LocalSaveFileNameRuleIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(0, OnLocalSaveFileNameRuleIndexChanged));

    public static readonly DependencyProperty LocalSaveFileNameCustomRuleProperty = DependencyProperty.Register(nameof(LocalSaveFileNameCustomRule), typeof(string), typeof(MainWindow), new PropertyMetadata("{主播名}_{录制时间}"));

    public static readonly DependencyProperty IsLocalSaveFileNameRuleCustomProperty = DependencyProperty.Register(nameof(IsLocalSaveFileNameRuleCustom), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public double RoomCardItemWidth
    {
        get => (double)GetValue(RoomCardItemWidthProperty);
        set => SetValue(RoomCardItemWidthProperty, value);
    }

    public double RoomCardItemHeight
    {
        get => (double)GetValue(RoomCardItemHeightProperty);
        set => SetValue(RoomCardItemHeightProperty, value);
    }

    public double RoomCardPanelWidth
    {
        get => (double)GetValue(RoomCardPanelWidthProperty);
        set => SetValue(RoomCardPanelWidthProperty, value);
    }

    public double RoomCardWidth
    {
        get => (double)GetValue(RoomCardWidthProperty);
        set => SetValue(RoomCardWidthProperty, value);
    }

    public double RoomCardHeight
    {
        get => (double)GetValue(RoomCardHeightProperty);
        set => SetValue(RoomCardHeightProperty, value);
    }

    public Thickness RoomCardPadding
    {
        get => (Thickness)GetValue(RoomCardPaddingProperty);
        set => SetValue(RoomCardPaddingProperty, value);
    }

    public Thickness RoomCardMargin
    {
        get => (Thickness)GetValue(RoomCardMarginProperty);
        set => SetValue(RoomCardMarginProperty, value);
    }

    public double RoomCardAvatarSize
    {
        get => (double)GetValue(RoomCardAvatarSizeProperty);
        set => SetValue(RoomCardAvatarSizeProperty, value);
    }

    public double RoomCardAvatarContainerSize
    {
        get => (double)GetValue(RoomCardAvatarContainerSizeProperty);
        set => SetValue(RoomCardAvatarContainerSizeProperty, value);
    }

    public double RoomCardAvatarIconSize
    {
        get => (double)GetValue(RoomCardAvatarIconSizeProperty);
        set => SetValue(RoomCardAvatarIconSizeProperty, value);
    }

    public GridLength RoomCardHeaderColumnWidth
    {
        get => (GridLength)GetValue(RoomCardHeaderColumnWidthProperty);
        set => SetValue(RoomCardHeaderColumnWidthProperty, value);
    }

    public Thickness RoomCardAvatarMargin
    {
        get => (Thickness)GetValue(RoomCardAvatarMarginProperty);
        set => SetValue(RoomCardAvatarMarginProperty, value);
    }

    public double RoomCardNameFontSize
    {
        get => (double)GetValue(RoomCardNameFontSizeProperty);
        set => SetValue(RoomCardNameFontSizeProperty, value);
    }

    public double RoomCardPlatformFontSize
    {
        get => (double)GetValue(RoomCardPlatformFontSizeProperty);
        set => SetValue(RoomCardPlatformFontSizeProperty, value);
    }

    public double RoomCardTitleFontSize
    {
        get => (double)GetValue(RoomCardTitleFontSizeProperty);
        set => SetValue(RoomCardTitleFontSizeProperty, value);
    }

    public double RoomCardTitleLineHeight
    {
        get => (double)GetValue(RoomCardTitleLineHeightProperty);
        set => SetValue(RoomCardTitleLineHeightProperty, value);
    }

    public double RoomCardTitleMaxHeight
    {
        get => (double)GetValue(RoomCardTitleMaxHeightProperty);
        set => SetValue(RoomCardTitleMaxHeightProperty, value);
    }

    public Visibility RoomCardTitleVisibility
    {
        get => (Visibility)GetValue(RoomCardTitleVisibilityProperty);
        set => SetValue(RoomCardTitleVisibilityProperty, value);
    }

    public double RoomCardChipFontSize
    {
        get => (double)GetValue(RoomCardChipFontSizeProperty);
        set => SetValue(RoomCardChipFontSizeProperty, value);
    }

    public Thickness RoomCardChipPadding
    {
        get => (Thickness)GetValue(RoomCardChipPaddingProperty);
        set => SetValue(RoomCardChipPaddingProperty, value);
    }

    public double RoomCardChipMinHeight
    {
        get => (double)GetValue(RoomCardChipMinHeightProperty);
        set => SetValue(RoomCardChipMinHeightProperty, value);
    }

    public bool CanUseLargeRoomCards
    {
        get => (bool)GetValue(CanUseLargeRoomCardsProperty);
        set => SetValue(CanUseLargeRoomCardsProperty, value);
    }

    public string RoutineIntervalSecondsText
    {
        get => (string)GetValue(RoutineIntervalSecondsTextProperty);
        set => SetValue(RoutineIntervalSecondsTextProperty, value);
    }

    public string RoutineIntervalMinutesText
    {
        get => (string)GetValue(RoutineIntervalMinutesTextProperty);
        set => SetValue(RoutineIntervalMinutesTextProperty, value);
    }

    public string SegmentTimeSecondsText
    {
        get => (string)GetValue(SegmentTimeSecondsTextProperty);
        set => SetValue(SegmentTimeSecondsTextProperty, value);
    }

    public string SegmentTimeMinutesText
    {
        get => (string)GetValue(SegmentTimeMinutesTextProperty);
        set => SetValue(SegmentTimeMinutesTextProperty, value);
    }

    public string SegmentTimeHoursText
    {
        get => (string)GetValue(SegmentTimeHoursTextProperty);
        set => SetValue(SegmentTimeHoursTextProperty, value);
    }

    public string SegmentTimeMegabytesText
    {
        get => (string)GetValue(SegmentTimeMegabytesTextProperty);
        set => SetValue(SegmentTimeMegabytesTextProperty, value);
    }

    public string SegmentTimeGigabytesText
    {
        get => (string)GetValue(SegmentTimeGigabytesTextProperty);
        set => SetValue(SegmentTimeGigabytesTextProperty, value);
    }

    public int LocalRecordFormatIndex
    {
        get => (int)GetValue(LocalRecordFormatIndexProperty);
        set => SetValue(LocalRecordFormatIndexProperty, value);
    }

    public int LocalStreamQualityIndex
    {
        get => (int)GetValue(LocalStreamQualityIndexProperty);
        set => SetValue(LocalStreamQualityIndexProperty, value);
    }

    public bool LocalIsToRecord
    {
        get => (bool)GetValue(LocalIsToRecordProperty);
        set => SetValue(LocalIsToRecordProperty, value);
    }

    public bool LocalIsToMonitor
    {
        get => (bool)GetValue(LocalIsToMonitorProperty);
        set => SetValue(LocalIsToMonitorProperty, value);
    }

    public bool LocalIsRemoveTs
    {
        get => (bool)GetValue(LocalIsRemoveTsProperty);
        set => SetValue(LocalIsRemoveTsProperty, value);
    }

    public bool LocalIsToSegment
    {
        get => (bool)GetValue(LocalIsToSegmentProperty);
        set => SetValue(LocalIsToSegmentProperty, value);
    }

    public double LocalSegmentTimeValue
    {
        get => (double)GetValue(LocalSegmentTimeValueProperty);
        set => SetValue(LocalSegmentTimeValueProperty, value);
    }

    public int LocalSegmentTimeUnitIndex
    {
        get => (int)GetValue(LocalSegmentTimeUnitIndexProperty);
        set => SetValue(LocalSegmentTimeUnitIndexProperty, value);
    }

    public double LocalRoutineIntervalValue
    {
        get => (double)GetValue(LocalRoutineIntervalValueProperty);
        set => SetValue(LocalRoutineIntervalValueProperty, value);
    }

    public int LocalRoutineIntervalUnitIndex
    {
        get => (int)GetValue(LocalRoutineIntervalUnitIndexProperty);
        set => SetValue(LocalRoutineIntervalUnitIndexProperty, value);
    }

    public int LocalRoutineScheduleModeIndex
    {
        get => (int)GetValue(LocalRoutineScheduleModeIndexProperty);
        set => SetValue(LocalRoutineScheduleModeIndexProperty, value);
    }

    public bool IsLocalRoutineScheduleCustom
    {
        get => (bool)GetValue(IsLocalRoutineScheduleCustomProperty);
        set => SetValue(IsLocalRoutineScheduleCustomProperty, value);
    }

    public bool IsLocalRoutineScheduleAlways
    {
        get => (bool)GetValue(IsLocalRoutineScheduleAlwaysProperty);
        set => SetValue(IsLocalRoutineScheduleAlwaysProperty, value);
    }

    public bool LocalRoutineScheduleMonday
    {
        get => (bool)GetValue(LocalRoutineScheduleMondayProperty);
        set => SetValue(LocalRoutineScheduleMondayProperty, value);
    }

    public bool LocalRoutineScheduleTuesday
    {
        get => (bool)GetValue(LocalRoutineScheduleTuesdayProperty);
        set => SetValue(LocalRoutineScheduleTuesdayProperty, value);
    }

    public bool LocalRoutineScheduleWednesday
    {
        get => (bool)GetValue(LocalRoutineScheduleWednesdayProperty);
        set => SetValue(LocalRoutineScheduleWednesdayProperty, value);
    }

    public bool LocalRoutineScheduleThursday
    {
        get => (bool)GetValue(LocalRoutineScheduleThursdayProperty);
        set => SetValue(LocalRoutineScheduleThursdayProperty, value);
    }

    public bool LocalRoutineScheduleFriday
    {
        get => (bool)GetValue(LocalRoutineScheduleFridayProperty);
        set => SetValue(LocalRoutineScheduleFridayProperty, value);
    }

    public bool LocalRoutineScheduleSaturday
    {
        get => (bool)GetValue(LocalRoutineScheduleSaturdayProperty);
        set => SetValue(LocalRoutineScheduleSaturdayProperty, value);
    }

    public bool LocalRoutineScheduleSunday
    {
        get => (bool)GetValue(LocalRoutineScheduleSundayProperty);
        set => SetValue(LocalRoutineScheduleSundayProperty, value);
    }

    public int LocalRoutineScheduleStartHour
    {
        get => (int)GetValue(LocalRoutineScheduleStartHourProperty);
        set => SetValue(LocalRoutineScheduleStartHourProperty, Math.Clamp(value, 0, 23));
    }

    public int LocalRoutineScheduleStartMinute
    {
        get => (int)GetValue(LocalRoutineScheduleStartMinuteProperty);
        set => SetValue(LocalRoutineScheduleStartMinuteProperty, Math.Clamp(value, 0, 59));
    }

    public int LocalRoutineScheduleEndHour
    {
        get => (int)GetValue(LocalRoutineScheduleEndHourProperty);
        set => SetValue(LocalRoutineScheduleEndHourProperty, Math.Clamp(value, 0, 23));
    }

    public int LocalRoutineScheduleEndMinute
    {
        get => (int)GetValue(LocalRoutineScheduleEndMinuteProperty);
        set => SetValue(LocalRoutineScheduleEndMinuteProperty, Math.Clamp(value, 0, 59));
    }

    public string LocalSaveFolder
    {
        get => (string)GetValue(LocalSaveFolderProperty);
        set => SetValue(LocalSaveFolderProperty, value);
    }

    public int LocalSaveFolderPathLevelIndex
    {
        get => (int)GetValue(LocalSaveFolderPathLevelIndexProperty);
        set => SetValue(LocalSaveFolderPathLevelIndexProperty, value);
    }

    public int LocalSaveFileNameRuleIndex
    {
        get => (int)GetValue(LocalSaveFileNameRuleIndexProperty);
        set => SetValue(LocalSaveFileNameRuleIndexProperty, Math.Clamp(value, 0, 4));
    }

    public string LocalSaveFileNameCustomRule
    {
        get => (string)GetValue(LocalSaveFileNameCustomRuleProperty);
        set => SetValue(LocalSaveFileNameCustomRuleProperty, string.IsNullOrWhiteSpace(value) ? "{主播名}_{录制时间}" : value);
    }

    public bool IsLocalSaveFileNameRuleCustom
    {
        get => (bool)GetValue(IsLocalSaveFileNameRuleCustomProperty);
        set => SetValue(IsLocalSaveFileNameRuleCustomProperty, value);
    }

    private static void OnLocalSaveFileNameRuleIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MainWindow window)
        {
            window.IsLocalSaveFileNameRuleCustom = (int)e.NewValue == 4;
        }
    }

    private static void OnLocalRoutineScheduleModeIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MainWindow window)
        {
            int mode = (int)e.NewValue;
            window.IsLocalRoutineScheduleCustom = mode == 1;
            window.IsLocalRoutineScheduleAlways = mode == 0;
        }
    }

    private const int RoomCardBaseColumns = 3;

    private const double RoomCardMinScale = 0.86d;

    private const double RoomCardMaxScale = 1.14d;

    private const double RoomCardBoundaryTolerance = 1d;

    private const double RoomCardLargeSizeScale = 1.5d;

    private const double RoomCardMediumSizeScale = 1d;

    private const double RoomCardSmallSizeScale = 0.5d;

    private const double RoomCardHorizontalGap = 8d;

    private const double RoomCardVerticalGap = 8d;

    private const int RoomCardDragDelayMilliseconds = 260;

    private const int RoomCardBlankLongPressMilliseconds = 560;

    private double roomCardBaseWidth;

    private bool isRoomCardBaseWidthCaptured;

    private double roomCardSizePreference = RoomCardMediumSizeScale;

    private System.Windows.Point roomCardDragStart;

    private DateTime roomCardPressedAt = DateTime.MinValue;

    private RoomStatusReactive? draggedRoom;

    private ListBoxItem? draggedRoomItem;

    private Point roomCardDragOffset;

    private bool isRoomCardDragging;

    private DispatcherTimer? roomCardBlankPressTimer;

    private bool roomCardBlankPressCandidate;

    private Point roomCardBlankPressStart;

    private AdornerLayer? roomCardAdornerLayer;

    private DragPreviewAdorner? roomCardDragAdorner;

    private InsertionLineAdorner? roomCardInsertionAdorner;

    private int roomCardInsertionIndex = -1;

    private int routineIntervalUnitIndex;

    private bool isUpdatingRoutineIntervalFlyout;

    private int developerUnlockClickCount;

    private DateTime developerUnlockClickStartedAt = DateTime.MinValue;

    private readonly BlurEffect modalBlurEffect = new() { Radius = 8 };

    private const double BottomFlyoutGap = 40d;

    private bool isExitStrategyPending;

    public MainWindow()
    {
        DataContext = ViewModel = new();
        WindowSizing.UseRelativeScreenSize(this, 1290d, 900d);
        InitializeComponent();
        PreviewMouseDown += MainWindowPreviewMouseDown;
        SizeChanged += MainWindowSizeChanged;
        StateChanged += MainWindowStateChanged;
        WeakReferenceMessenger.Default.Register<RoomCardsFlashMessage>(this, (_, _) =>
        {
            Dispatcher.BeginInvoke(BeginRoomCardsFlashAnimation, DispatcherPriority.Background);
        });
        WeakReferenceMessenger.Default.Register<AutoShutdownPromptMessage>(this, (_, _) =>
        {
            Dispatcher.BeginInvoke(() => OpenCenteredFlyout(AutoShutdownPromptFlyout), DispatcherPriority.Background);
        });
        Locale.CultureChanged += LocaleCultureChanged;
        RefreshRoutineIntervalUnitTexts();

        routineIntervalUnitIndex = RoutineIntervalUnitHelper.GetPreferredUnitIndex(ViewModel.StatusOfRoutineInterval);

        if (Configurations.IsUseKeepAwake.Get())
        {
            // Start keep awake
            _ = Kernel32.SetThreadExecutionState(Kernel32.EXECUTION_STATE.ES_CONTINUOUS | Kernel32.EXECUTION_STATE.ES_SYSTEM_REQUIRED | Kernel32.EXECUTION_STATE.ES_AWAYMODE_REQUIRED);
        }

        if (Environment.GetCommandLineArgs().Any(cli => cli == "/autorun"))
        {
            Visibility = System.Windows.Visibility.Hidden;
            WindowState = System.Windows.WindowState.Minimized;
        }
    }

    private void RoomCardListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRoomCardMetrics(e.NewSize.Width);
    }

    private void RoomCardListLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CaptureRoomCardBaseWidth(RoomCardList.ActualWidth);
            UpdateRoomCardMetrics(RoomCardList.ActualWidth);
        }, DispatcherPriority.Loaded);
    }

    private void UpdateRoomCardMetrics(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            return;
        }

        double availableWidth = GetRoomCardAvailableWidth(width);
        double baseWidth = GetRoomCardBaseWidth(availableWidth);
        roomCardSizePreference = NormalizeRoomCardScale(availableWidth, baseWidth, roomCardSizePreference);
        (int columns, double slotWidth, double cardWidth) = CalculateRoomCardLayout(availableWidth, baseWidth, roomCardSizePreference, RoomCardHorizontalGap);
        double cardHeight = Math.Floor(cardWidth * 2d / 3d);
        double itemHeight = cardHeight + RoomCardVerticalGap;

        RoomCardItemWidth = slotWidth;
        RoomCardItemHeight = itemHeight;
        RoomCardPanelWidth = slotWidth * columns;
        RoomCardWidth = cardWidth;
        RoomCardHeight = cardHeight;
        RoomCardMargin = new Thickness(RoomCardHorizontalGap / 2d, RoomCardVerticalGap / 2d, RoomCardHorizontalGap / 2d, RoomCardVerticalGap / 2d);
        CanUseLargeRoomCards = CanUseRoomCardScale(availableWidth, baseWidth, RoomCardLargeSizeScale, RoomCardHorizontalGap);
        UpdateRoomCardVisualMetrics(cardWidth, baseWidth);
    }

    private static double GetRoomCardAvailableWidth(double width)
    {
        return Math.Max(90d, width);
    }

    private void CaptureRoomCardBaseWidth(double width)
    {
        if (isRoomCardBaseWidthCaptured)
        {
            return;
        }

        double availableWidth = GetRoomCardAvailableWidth(width);
        roomCardBaseWidth = Math.Max(1d, (availableWidth - RoomCardHorizontalGap * RoomCardBaseColumns) / RoomCardBaseColumns);
        isRoomCardBaseWidthCaptured = true;
    }

    private double GetRoomCardBaseWidth(double availableWidth)
    {
        return isRoomCardBaseWidthCaptured ? roomCardBaseWidth : Math.Max(1d, (availableWidth - RoomCardHorizontalGap * RoomCardBaseColumns) / RoomCardBaseColumns);
    }

    private double NormalizeRoomCardScale(double availableWidth, double baseWidth, double preference)
    {
        if (preference > RoomCardMediumSizeScale && !CanUseRoomCardScale(availableWidth, baseWidth, preference, RoomCardHorizontalGap))
        {
            return RoomCardMediumSizeScale;
        }

        if (preference >= RoomCardMediumSizeScale && !CanUseRoomCardScale(availableWidth, baseWidth, RoomCardMediumSizeScale, RoomCardHorizontalGap))
        {
            return RoomCardSmallSizeScale;
        }

        return preference;
    }

    private static bool CanUseRoomCardScale(double availableWidth, double baseWidth, double preference, double horizontalGap)
    {
        double targetWidth = Math.Max(1d, baseWidth * preference);
        return availableWidth >= targetWidth * RoomCardMinScale + horizontalGap;
    }

    private static (int Columns, double SlotWidth, double CardWidth) CalculateRoomCardLayout(double availableWidth, double baseWidth, double preference, double horizontalGap)
    {
        double targetWidth = Math.Max(1d, baseWidth * preference);
        double minWidth = targetWidth * RoomCardMinScale;
        double maxWidth = targetWidth * RoomCardMaxScale;
        double minSlotWidth = minWidth + horizontalGap;
        double maxSlotWidth = maxWidth + horizontalGap;
        double preferredSlotWidth = targetWidth + horizontalGap;
        int columns = Math.Max(1, (int)Math.Ceiling(availableWidth / preferredSlotWidth));
        double slotWidth = availableWidth / columns;
        double cardWidth = Math.Max(1d, slotWidth - horizontalGap);

        while (cardWidth > maxWidth)
        {
            columns++;
            slotWidth = availableWidth / columns;
            cardWidth = Math.Max(1d, slotWidth - horizontalGap);
        }

        while (columns > 1 && cardWidth < minWidth - RoomCardBoundaryTolerance)
        {
            columns--;
            slotWidth = availableWidth / columns;
            cardWidth = Math.Max(1d, slotWidth - horizontalGap);
        }

        if (cardWidth > maxWidth)
        {
            cardWidth = maxWidth;
            slotWidth = Math.Min(maxSlotWidth, cardWidth + horizontalGap);
        }

        if (cardWidth < minWidth - RoomCardBoundaryTolerance && availableWidth >= minSlotWidth)
        {
            cardWidth = minWidth;
            slotWidth = cardWidth + horizontalGap;
        }

        return (columns, slotWidth, cardWidth);
    }

    private void UpdateRoomCardVisualMetrics(double cardWidth, double baseWidth)
    {
        double scale = Math.Clamp(cardWidth / baseWidth, RoomCardSmallSizeScale * RoomCardMinScale, RoomCardLargeSizeScale * RoomCardMaxScale);
        double chipHeight = Math.Clamp((cardWidth - 18d) / 4d, 14d, 42d);

        RoomCardPadding = new Thickness(Math.Round(8d * scale));
        RoomCardAvatarContainerSize = Math.Round(38d * scale);
        RoomCardAvatarSize = Math.Round(36d * scale);
        RoomCardAvatarIconSize = Math.Round(20d * scale);
        RoomCardAvatarMargin = new Thickness(Math.Round(3d * scale), Math.Round(3d * scale), Math.Round(10d * scale), 0);
        RoomCardHeaderColumnWidth = new GridLength(Math.Round(54d * scale));
        RoomCardNameFontSize = Math.Max(8d, Math.Round(15d * scale));
        RoomCardPlatformFontSize = Math.Max(7d, Math.Round(12d * scale));
        RoomCardTitleFontSize = Math.Max(7d, Math.Round(12d * scale));
        RoomCardTitleLineHeight = Math.Max(9d, Math.Round(16d * scale));
        RoomCardTitleMaxHeight = Math.Round(32d * scale);
        RoomCardTitleVisibility = scale < 0.72d ? Visibility.Collapsed : Visibility.Visible;
        RoomCardChipFontSize = Math.Max(7d, Math.Round(11d * scale));
        RoomCardChipPadding = new Thickness(Math.Round(6d * scale), Math.Round(4d * scale), Math.Round(6d * scale), Math.Round(4d * scale));
        RoomCardChipMinHeight = chipHeight;
    }

    private void SetRoomCardLargeClick(object sender, RoutedEventArgs e)
    {
        SetRoomCardScale(RoomCardLargeSizeScale);
    }

    private void SetRoomCardMediumClick(object sender, RoutedEventArgs e)
    {
        SetRoomCardScale(RoomCardMediumSizeScale);
    }

    private void SetRoomCardSmallClick(object sender, RoutedEventArgs e)
    {
        SetRoomCardScale(RoomCardSmallSizeScale);
    }

    private void SetRoomCardScale(double scale)
    {
        double availableWidth = GetRoomCardAvailableWidth(RoomCardList.ActualWidth);
        double baseWidth = GetRoomCardBaseWidth(availableWidth);

        if (scale > RoomCardMediumSizeScale && !CanUseRoomCardScale(availableWidth, baseWidth, scale, RoomCardHorizontalGap))
        {
            return;
        }

        roomCardSizePreference = Math.Clamp(scale, RoomCardSmallSizeScale, RoomCardLargeSizeScale);
        UpdateRoomCardMetrics(RoomCardList.ActualWidth);
    }

    private void RoomCardListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        CancelRoomCardBlankPress();
        ListBoxItem? item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        roomCardDragStart = e.GetPosition(RoomCardList);
        roomCardPressedAt = DateTime.Now;

        if (item == null)
        {
            draggedRoom = null;
            draggedRoomItem = null;
            StartRoomCardBlankPress(roomCardDragStart);
            return;
        }

        draggedRoom = ViewModel.IsCardEditMode ? item.DataContext as RoomStatusReactive : null;
        draggedRoomItem = draggedRoom == null ? null : item;
        roomCardDragOffset = e.GetPosition(item);
    }

    private void RoomCardListPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Point currentPosition = e.GetPosition(RoomCardList);

        if (isRoomCardDragging)
        {
            UpdateRoomCardDrag(currentPosition);
            e.Handled = true;
            return;
        }

        if (roomCardBlankPressCandidate)
        {
            bool movedBlank = Math.Abs(currentPosition.X - roomCardBlankPressStart.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - roomCardBlankPressStart.Y) >= SystemParameters.MinimumVerticalDragDistance;

            if (movedBlank || e.LeftButton != MouseButtonState.Pressed)
            {
                CancelRoomCardBlankPress();
            }

            return;
        }

        if (!ViewModel.IsCardEditMode || e.LeftButton != MouseButtonState.Pressed || draggedRoom == null || draggedRoomItem == null)
        {
            return;
        }

        bool isHorizontalDrag = Math.Abs(currentPosition.X - roomCardDragStart.X) >= SystemParameters.MinimumHorizontalDragDistance;
        bool isVerticalDrag = Math.Abs(currentPosition.Y - roomCardDragStart.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if ((!isHorizontalDrag && !isVerticalDrag) ||
            (DateTime.Now - roomCardPressedAt).TotalMilliseconds < RoomCardDragDelayMilliseconds)
        {
            return;
        }

        StartRoomCardDrag(currentPosition);
        e.Handled = true;
    }

    private void RoomCardListPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isRoomCardDragging)
        {
            FinishRoomCardDrag(true);
            e.Handled = true;
            return;
        }

        CancelRoomCardBlankPress();
        draggedRoom = null;
        draggedRoomItem = null;
    }

    private void RoomCardListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelRoomCardBlankPress();

        if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject) is ListBoxItem item &&
            item.DataContext is RoomStatusReactive room)
        {
            RoomCardList.SelectedItem = room;
            ViewModel.SelectedItem = room;
            item.Focus();
            return;
        }

        RoomCardPanel.ContextMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, true);
        e.Handled = true;
    }

    private void RoomCardListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        CancelRoomCardBlankPress();

        if (ViewModel.RefreshRoomCardsCommand.CanExecute(null))
        {
            ViewModel.RefreshRoomCardsCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void RoomCardPanelMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            RoomCardListMouseDoubleClick(sender, e);
        }
    }

    private void OpenScreenRecordListClick(object sender, RoutedEventArgs e)
    {
        foreach (Window win in Application.Current.Windows)
        {
            if (win is ScreenRecordListWindow)
            {
                win.Activate();
                return;
            }
        }

        new ScreenRecordListWindow
        {
            Owner = Application.Current.MainWindow,
        }.ShowDialog();
    }

    private void BeginRoomCardsFlashAnimation()
    {
        foreach (ListBoxItem item in GetRoomCardItems())
        {
            if (FindVisualChild<System.Windows.Controls.Border>(item, "RoomCardFlashOverlay") is not System.Windows.Controls.Border overlay)
            {
                continue;
            }

            overlay.Margin = new Thickness(-RoomCardPadding.Left, -RoomCardPadding.Top, -RoomCardPadding.Right, -RoomCardPadding.Bottom);
            overlay.BeginAnimation(OpacityProperty, null);
            DoubleAnimationUsingKeyFrames animation = new()
            {
                Duration = TimeSpan.FromSeconds(0.6),
                FillBehavior = FillBehavior.Stop,
            };
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.82, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
            overlay.BeginAnimation(OpacityProperty, animation);
        }
    }

    private List<ListBoxItem> GetRoomCardItems()
    {
        RoomCardList.UpdateLayout();
        List<ListBoxItem> items = [];

        for (int index = 0; index < RoomCardList.Items.Count; index++)
        {
            if (RoomCardList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private void RoutineIntervalStatusButtonClick(object sender, RoutedEventArgs e)
    {
        SetRoutineIntervalFlyoutValue(ViewModel.StatusOfRoutineInterval);
        OpenBoundedFlyout(RoutineIntervalFlyout, RoutineIntervalStatusButton);
    }

    private void LocaleCultureChanged(object? sender, EventArgs e)
    {
        RefreshRoutineIntervalUnitTexts();
    }

    private void RefreshRoutineIntervalUnitTexts()
    {
        RoutineIntervalSecondsText = RoutineIntervalUnitHelper.GetUnitText(RoutineIntervalUnitHelper.Seconds);
        RoutineIntervalMinutesText = RoutineIntervalUnitHelper.GetUnitText(RoutineIntervalUnitHelper.Minutes);
        SegmentTimeSecondsText = SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Seconds);
        SegmentTimeMinutesText = SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Minutes);
        SegmentTimeHoursText = SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Hours);
        SegmentTimeMegabytesText = SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Megabytes);
        SegmentTimeGigabytesText = SegmentTimeUnitHelper.GetUnitText(SegmentTimeUnitHelper.Gigabytes);
    }

    private void RecordFormatStatusTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenBoundedFlyout(RecordFormatFlyout, RecordFormatStatusText);
        e.Handled = true;
    }

    private void AutoShutdownTimeStatusButtonClick(object sender, RoutedEventArgs e)
    {
        SetAutoShutdownTimeFlyoutValue(ViewModel.StatusOfAutoShutdownTime);
        OpenBoundedFlyout(AutoShutdownTimeFlyout, AutoShutdownTimeStatusButton);
    }

    private void RoutineIntervalConfirmClick(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(RoutineIntervalInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            int milliseconds = RoutineIntervalUnitHelper.ToMilliseconds(value, routineIntervalUnitIndex);
            string interval = milliseconds.ToString(CultureInfo.InvariantCulture);

            if (ViewModel.SetRoutineIntervalCommand.CanExecute(interval))
            {
                ViewModel.SetRoutineIntervalCommand.Execute(interval);
            }
        }

        RoutineIntervalFlyout.Visibility = Visibility.Collapsed;
    }

    private void RoutineIntervalInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        RoutineIntervalConfirmClick(sender, e);
        e.Handled = true;
    }

    private void AutoShutdownTimeConfirmClick(object sender, RoutedEventArgs e)
    {
        int hour = ParseClampedInteger(AutoShutdownHourInput.Text, 0, 23);
        int minute = ParseClampedInteger(AutoShutdownMinuteInput.Text, 0, 59);
        string value = $"{hour:D2}:{minute:D2}";

        if (ViewModel.SetAutoShutdownTimeCommand.CanExecute(value))
        {
            ViewModel.SetAutoShutdownTimeCommand.Execute(value);
        }

        AutoShutdownTimeFlyout.Visibility = Visibility.Collapsed;
    }

    private void AutoShutdownTimeInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        AutoShutdownTimeConfirmClick(sender, e);
        e.Handled = true;
    }

    private void RoutineIntervalUnitSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingRoutineIntervalFlyout || RoutineIntervalUnitComboBox.SelectedIndex < 0)
        {
            return;
        }

        if (!double.TryParse(RoutineIntervalInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            value = RoutineIntervalUnitHelper.ToDisplayValue(ViewModel.StatusOfRoutineInterval, routineIntervalUnitIndex);
        }

        int previousUnitIndex = routineIntervalUnitIndex;
        routineIntervalUnitIndex = RoutineIntervalUnitComboBox.SelectedIndex;
        int milliseconds = RoutineIntervalUnitHelper.ToMilliseconds(value, previousUnitIndex);
        double displayValue = RoutineIntervalUnitHelper.ToDisplayValue(milliseconds, routineIntervalUnitIndex);

        isUpdatingRoutineIntervalFlyout = true;
        try
        {
            RoutineIntervalInput.Text = displayValue.ToString("0.###", CultureInfo.InvariantCulture);
        }
        finally
        {
            isUpdatingRoutineIntervalFlyout = false;
        }
    }

    private void NumberInputPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = sender is not System.Windows.Controls.TextBox textBox || !IsAllowedNumberInput(textBox, e.Text);
    }

    private void NumberInputPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Space)
        {
            e.Handled = true;
        }
    }

    private void NumberInputPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox ||
            !e.DataObject.GetDataPresent(System.Windows.DataFormats.Text) ||
            e.DataObject.GetData(System.Windows.DataFormats.Text) is not string text ||
            !IsAllowedNumberInput(textBox, text))
        {
            e.CancelCommand();
        }
    }

    private static bool IsAllowedNumberInput(System.Windows.Controls.TextBox textBox, string input)
    {
        string value = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, input);

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        bool allowDecimal = string.Equals((textBox.Tag as string) ?? string.Empty, "Decimal", StringComparison.OrdinalIgnoreCase);

        return allowDecimal
            ? double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _)
            : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private void RecordFormatOptionClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string value } &&
            ViewModel.SetRecordFormatCommand.CanExecute(value))
        {
            ViewModel.SetRecordFormatCommand.Execute(value);
        }

        RecordFormatFlyout.Visibility = Visibility.Collapsed;
    }

    private void OpenAddRoomFlyoutClick(object sender, RoutedEventArgs e)
    {
        AddRoomUrlInput.Text = string.Empty;
        AddRoomForceCheckBox.IsChecked = false;
        AddRoomFollowGlobalSettingsCheckBox.IsChecked = true;
        AddRoomNotifyCheckBox.IsChecked = true;
        LoadLocalSettingsFlyoutValues(null);
        ApplyAddRoomFlyoutMode();
        OpenCenteredFlyout(AddRoomFlyout);
        Dispatcher.BeginInvoke(() => AddRoomUrlInput.Focus(), DispatcherPriority.Input);
    }

    private void OpenAboutFlyoutClick(object sender, RoutedEventArgs e)
    {
        OpenCenteredFlyout(AboutFlyout);
    }

    private async void CheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        await AppUpdater.CheckAsync(showNoUpdateMessage: true);
    }

    private void AboutVersionTextBlockMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        DateTime now = DateTime.Now;

        if ((now - developerUnlockClickStartedAt).TotalSeconds > 2.5)
        {
            developerUnlockClickStartedAt = now;
            developerUnlockClickCount = 0;
        }

        developerUnlockClickCount++;

        if (developerUnlockClickCount < 5)
        {
            return;
        }

        developerUnlockClickCount = 0;
        DeveloperModeManager.SetEnabled(true);
        Toast.Success("开发者模式已启用");
        e.Handled = true;
    }

    private void AddRoomFollowGlobalSettingsChanged(object sender, RoutedEventArgs e)
    {
        if (AddRoomFlyout == null)
        {
            return;
        }

        ApplyAddRoomFlyoutMode();
        Dispatcher.BeginInvoke(() => CenterVisibleFlyout(AddRoomFlyout), DispatcherPriority.Loaded);
    }

    private void ApplyAddRoomFlyoutMode()
    {
        bool showLocalSettings = AddRoomFollowGlobalSettingsCheckBox?.IsChecked == false;
        AddRoomFlyout.Width = showLocalSettings ? 654d : 540d;
        AddRoomFlyout.Height = showLocalSettings ? 724d : double.NaN;
        AddRoomLocalSettingsSeparator.Visibility = showLocalSettings ? Visibility.Visible : Visibility.Collapsed;
        AddRoomLocalSettingsPanel.Visibility = showLocalSettings ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenLocalSettingsFlyoutClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem == null)
        {
            Toast.Warning("请先选择直播间");
            return;
        }

        LoadLocalSettingsFlyoutValues(GetSelectedRoom());
        OpenCenteredFlyout(LocalSettingsFlyout);
    }

    private void LoadLocalSettingsFlyoutValues(Room? room)
    {
        RoomRecordingOptions settings = room == null ? RoomRecordingSettings.GetGlobal() : RoomRecordingSettings.Get(room);
        LocalRecordFormatIndex = settings.RecordFormat switch
        {
            "TS/FLV -> MP4" => 1,
            "TS/FLV -> MKV" => 2,
            _ => 0,
        };
        LocalStreamQualityIndex = settings.StreamQuality.ToUpperInvariant() switch
        {
            "BD" => 1,
            "UHD" => 2,
            "HD" => 3,
            "SD" => 4,
            "LD" => 5,
            _ => 0,
        };
        LocalIsToMonitor = room == null ? true : GlobalMonitor.GetEffectiveRoomMonitor(room);
        LocalIsToRecord = room == null ? Configurations.IsToRecord.Get() : GlobalMonitor.GetEffectiveRoomRecord(room);
        LocalIsRemoveTs = settings.IsRemoveTs;
        LocalIsToSegment = settings.IsToSegment;
        LocalSegmentTimeUnitIndex = settings.SegmentTimeUnit is >= SegmentTimeUnitHelper.Seconds and <= SegmentTimeUnitHelper.Gigabytes
            ? settings.SegmentTimeUnit
            : SegmentTimeUnitHelper.GetPreferredUnitIndex(settings.SegmentTime);
        LocalSegmentTimeValue = SegmentTimeUnitHelper.ToDisplayValue(settings.SegmentTime, LocalSegmentTimeUnitIndex);
        LocalRoutineIntervalUnitIndex = RoutineIntervalUnitHelper.GetPreferredUnitIndex(settings.RoutineInterval);
        LocalRoutineIntervalValue = RoutineIntervalUnitHelper.ToDisplayValue(settings.RoutineInterval, LocalRoutineIntervalUnitIndex);
        LocalRoutineScheduleModeIndex = Math.Clamp(settings.RoutineScheduleMode, 0, 1);
        HashSet<int> routineScheduleDays = ParseRoutineScheduleDays(settings.RoutineScheduleDays);
        LocalRoutineScheduleMonday = routineScheduleDays.Contains((int)DayOfWeek.Monday);
        LocalRoutineScheduleTuesday = routineScheduleDays.Contains((int)DayOfWeek.Tuesday);
        LocalRoutineScheduleWednesday = routineScheduleDays.Contains((int)DayOfWeek.Wednesday);
        LocalRoutineScheduleThursday = routineScheduleDays.Contains((int)DayOfWeek.Thursday);
        LocalRoutineScheduleFriday = routineScheduleDays.Contains((int)DayOfWeek.Friday);
        LocalRoutineScheduleSaturday = routineScheduleDays.Contains((int)DayOfWeek.Saturday);
        LocalRoutineScheduleSunday = routineScheduleDays.Contains((int)DayOfWeek.Sunday);
        LocalRoutineScheduleStartHour = Math.Clamp(settings.RoutineScheduleStartHour, 0, 23);
        LocalRoutineScheduleStartMinute = Math.Clamp(settings.RoutineScheduleStartMinute, 0, 59);
        LocalRoutineScheduleEndHour = Math.Clamp(settings.RoutineScheduleEndHour, 0, 23);
        LocalRoutineScheduleEndMinute = Math.Clamp(settings.RoutineScheduleEndMinute, 0, 59);
        LocalSaveFolder = settings.SaveFolder;
        LocalSaveFolderPathLevelIndex = Math.Clamp(settings.SaveFolderPathLevel, 0, 1);
        LocalSaveFileNameRuleIndex = Math.Clamp(settings.SaveFileNameRule, 0, 4);
        LocalSaveFileNameCustomRule = string.IsNullOrWhiteSpace(settings.SaveFileNameCustomRule)
            ? "{主播名}_{录制时间}"
            : settings.SaveFileNameCustomRule;
    }

    private static HashSet<int> ParseRoutineScheduleDays(string? value)
    {
        HashSet<int> days = [];

        foreach (string item in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(item, out int day) && day is >= 0 and <= 6)
            {
                days.Add(day);
            }
        }

        return days.Count > 0 ? days : [1, 2, 3, 4, 5, 6, 0];
    }

    private void SelectLocalSaveFolderClick(object sender, RoutedEventArgs e)
    {
        using CommonOpenFileDialog dialog = new()
        {
            IsFolderPicker = true,
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            LocalSaveFolder = dialog.FileName;
        }
    }

    private async void OpenLocalSaveFolderClick(object sender, RoutedEventArgs e)
    {
        string folder = SaveFolderHelper.GetSaveFolder(LocalSaveFolder);
        if (!Directory.Exists(folder))
        {
            Toast.Warning("FolderNotExists".Tr());
            return;
        }

        await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(folder));
    }

    private void AppendLocalSaveFileNameTokenClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string token } || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        LocalSaveFileNameCustomRule = string.IsNullOrWhiteSpace(LocalSaveFileNameCustomRule)
            ? token
            : LocalSaveFileNameCustomRule.EndsWith('_')
                ? LocalSaveFileNameCustomRule + token
                : LocalSaveFileNameCustomRule + "_" + token;
    }

    private void DeleteLocalSaveFileNameTokenClick(object sender, RoutedEventArgs e)
    {
        string[] tokens = ["{主播uid}", "{主播名}", "{录制时间}", "{分辨率}", "{平台}"];

        foreach (string token in tokens.OrderByDescending(static value => value.Length))
        {
            if (LocalSaveFileNameCustomRule.EndsWith(token, StringComparison.Ordinal))
            {
                string value = LocalSaveFileNameCustomRule[..^token.Length];
                LocalSaveFileNameCustomRule = value.EndsWith('_') ? value[..^1] : value;
                return;
            }
        }

        if (LocalSaveFileNameCustomRule.Length > 0)
        {
            LocalSaveFileNameCustomRule = LocalSaveFileNameCustomRule[..^1];
        }
    }

    private void ResetLocalSaveFileNameRuleClick(object sender, RoutedEventArgs e)
    {
        LocalSaveFileNameCustomRule = "{主播名}_{录制时间}";
    }

    private void SaveLocalSettingsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveSelectedRoomLocalSettings(LocalIsToMonitor, LocalIsToRecord, BuildLocalRecordingOptions()))
        {
            ViewModel.ReloadConfigStatus();
            GlobalMonitor.RoutinePeriodicWait = new PeriodicWait(TimeSpan.FromMilliseconds(GetMinimumRoutineInterval()), TimeSpan.Zero);
            CloseFloatingPanels();
            Toast.Success("SuccOp".Tr());
        }
    }

    private RoomRecordingOptions BuildLocalRecordingOptions()
    {
        return new RoomRecordingOptions
        {
            RecordFormat = LocalRecordFormatIndex switch
            {
                1 => "TS/FLV -> MP4",
                2 => "TS/FLV -> MKV",
                _ => "TS/FLV",
            },
            StreamQuality = LocalStreamQualityIndex switch
            {
                1 => "BD",
                2 => "UHD",
                3 => "HD",
                4 => "SD",
                5 => "LD",
                _ => "OD",
            },
            IsRemoveTs = LocalIsRemoveTs,
            IsToSegment = LocalIsToSegment,
            SegmentTime = SegmentTimeUnitHelper.ToSeconds(LocalSegmentTimeValue, LocalSegmentTimeUnitIndex),
            SegmentTimeUnit = LocalSegmentTimeUnitIndex,
            RoutineInterval = RoutineIntervalUnitHelper.ToMilliseconds(LocalRoutineIntervalValue, LocalRoutineIntervalUnitIndex),
            RoutineScheduleMode = Math.Clamp(LocalRoutineScheduleModeIndex, 0, 1),
            RoutineScheduleDays = BuildLocalRoutineScheduleDays(),
            RoutineScheduleStartHour = LocalRoutineScheduleStartHour,
            RoutineScheduleStartMinute = LocalRoutineScheduleStartMinute,
            RoutineScheduleEndHour = LocalRoutineScheduleEndHour,
            RoutineScheduleEndMinute = LocalRoutineScheduleEndMinute,
            SaveFolder = LocalSaveFolder,
            SaveFolderPathLevel = Math.Clamp(LocalSaveFolderPathLevelIndex, 0, 1),
            SaveFileNameRule = Math.Clamp(LocalSaveFileNameRuleIndex, 0, 4),
            SaveFileNameCustomRule = LocalSaveFileNameCustomRule,
        };
    }

    private static int GetMinimumRoutineInterval()
    {
        int interval = Math.Max(500, Configurations.RoutineInterval.Get());
        foreach (Room room in Configurations.Rooms.Get())
        {
            interval = Math.Min(interval, Math.Max(500, RoomRecordingSettings.Get(room).RoutineInterval));
        }

        return interval;
    }

    private Room? GetSelectedRoom()
    {
        string? roomUrl = ViewModel.SelectedItem?.RoomUrl;
        return string.IsNullOrWhiteSpace(roomUrl)
            ? null
            : Configurations.Rooms.Get().FirstOrDefault(room => string.Equals(room.RoomUrl, roomUrl, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildLocalRoutineScheduleDays()
    {
        List<int> days = [];

        if (LocalRoutineScheduleMonday)
        {
            days.Add((int)DayOfWeek.Monday);
        }

        if (LocalRoutineScheduleTuesday)
        {
            days.Add((int)DayOfWeek.Tuesday);
        }

        if (LocalRoutineScheduleWednesday)
        {
            days.Add((int)DayOfWeek.Wednesday);
        }

        if (LocalRoutineScheduleThursday)
        {
            days.Add((int)DayOfWeek.Thursday);
        }

        if (LocalRoutineScheduleFriday)
        {
            days.Add((int)DayOfWeek.Friday);
        }

        if (LocalRoutineScheduleSaturday)
        {
            days.Add((int)DayOfWeek.Saturday);
        }

        if (LocalRoutineScheduleSunday)
        {
            days.Add((int)DayOfWeek.Sunday);
        }

        return days.Count > 0 ? string.Join(",", days) : "1,2,3,4,5,6,0";
    }

    private async void AddRoomConfirmClick(object sender, RoutedEventArgs e)
    {
        if (AddRoomLoadingOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        SetAddRoomLoading(true);
        try
        {
            bool added = await ViewModel.TryAddRoomFromFlyoutAsync(
                AddRoomUrlInput.Text,
                AddRoomForceCheckBox.IsChecked == true,
                AddRoomNotifyCheckBox.IsChecked == true,
                AddRoomFollowGlobalSettingsCheckBox.IsChecked == true,
                LocalIsToMonitor,
                LocalIsToRecord,
                AddRoomFollowGlobalSettingsCheckBox.IsChecked == true ? null : BuildLocalRecordingOptions());
            if (added)
            {
                AddRoomFlyout.Visibility = Visibility.Collapsed;
                UpdateModalOverlay();
            }
        }
        finally
        {
            SetAddRoomLoading(false);
        }
    }

    private void SetAddRoomLoading(bool isLoading)
    {
        AddRoomLoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        AddRoomConfirmButton.IsEnabled = !isLoading;
        AddRoomUrlInput.IsEnabled = !isLoading;
        AddRoomForceCheckBox.IsEnabled = !isLoading;
        AddRoomFollowGlobalSettingsCheckBox.IsEnabled = !isLoading;
        AddRoomNotifyCheckBox.IsEnabled = !isLoading;
    }

    private void AddRoomUrlInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        AddRoomConfirmClick(sender, e);
        e.Handled = true;
    }

    private void CloseFloatingPanelsClick(object sender, RoutedEventArgs e)
    {
        CloseFloatingPanels();
    }

    private void OpenBoundedFlyout(FrameworkElement flyout, FrameworkElement target)
    {
        CloseFloatingPanels(flyout);
        flyout.Visibility = Visibility.Visible;
        flyout.UpdateLayout();
        MainFlyoutLayer.UpdateLayout();

        Point targetPosition = GetRelativePosition(target, MainFlyoutLayer);
        double targetWidth = Math.Max(target.ActualWidth, 1);
        double targetHeight = Math.Max(target.ActualHeight, 1);
        double flyoutWidth = Math.Max(flyout.ActualWidth, flyout.DesiredSize.Width);
        double flyoutHeight = Math.Max(flyout.ActualHeight, flyout.DesiredSize.Height);
        double layerWidth = Math.Max(MainFlyoutLayer.ActualWidth, 1);
        double layerHeight = Math.Max(MainFlyoutLayer.ActualHeight, 1);
        double left = targetPosition.X + targetWidth - flyoutWidth;
        double anchorBottom = targetPosition.Y + targetHeight;

        if (target.Parent is FrameworkElement parent)
        {
            Point parentPosition = GetRelativePosition(parent, MainFlyoutLayer);
            if (parent.ActualHeight > targetHeight && parentPosition.Y <= targetPosition.Y)
            {
                anchorBottom = parentPosition.Y + parent.ActualHeight;
            }
        }

        double top = anchorBottom - flyoutHeight - BottomFlyoutGap;

        if (top < 0)
        {
            top = targetPosition.Y + targetHeight + BottomFlyoutGap;
        }

        left = Math.Clamp(left, 0, Math.Max(0, layerWidth - flyoutWidth));
        top = Math.Clamp(top, 0, Math.Max(0, layerHeight - flyoutHeight));

        Canvas.SetLeft(flyout, left);
        Canvas.SetTop(flyout, top);
    }

    private void OpenCenteredFlyout(FrameworkElement flyout)
    {
        if (flyout == null)
        {
            return;
        }

        CloseFloatingPanels(flyout);
        flyout.Visibility = Visibility.Visible;
        UpdateModalOverlay();
        Dispatcher.BeginInvoke(() => CenterVisibleFlyout(flyout), DispatcherPriority.Loaded);
    }

    private void ModalOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        CloseFloatingPanels();
        e.Handled = true;
    }

    private void CenterVisibleFlyout(FrameworkElement flyout)
    {
        if (flyout.Visibility != Visibility.Visible || MainFlyoutLayer == null)
        {
            return;
        }

        flyout.UpdateLayout();
        MainFlyoutLayer.UpdateLayout();

        double originalWidth = GetOriginalFlyoutWidth(flyout);
        double originalHeight = GetOriginalFlyoutHeight(flyout);
        double layerWidth = MainFlyoutLayer.ActualWidth > 1 ? MainFlyoutLayer.ActualWidth : Math.Max(ActualWidth, 1);
        double layerHeight = MainFlyoutLayer.ActualHeight > 1 ? MainFlyoutLayer.ActualHeight : Math.Max(ActualHeight, 1);
        double scale = GetFlyoutScale(originalWidth, originalHeight, layerWidth, layerHeight);
        flyout.LayoutTransform = scale < 1d ? new ScaleTransform(scale, scale) : null;
        flyout.UpdateLayout();

        double flyoutWidth = originalWidth * scale;
        double flyoutHeight = originalHeight * scale;
        double left = Math.Clamp((layerWidth - flyoutWidth) / 2d, 0, Math.Max(0, layerWidth - flyoutWidth));
        double top = Math.Clamp((layerHeight - flyoutHeight) / 2d, 0, Math.Max(0, layerHeight - flyoutHeight));

        Canvas.SetLeft(flyout, left);
        Canvas.SetTop(flyout, top);
    }

    private static double GetOriginalFlyoutWidth(FrameworkElement flyout)
    {
        double width = flyout.Width;
        if (double.IsNaN(width) || width <= 0)
        {
            width = Math.Max(flyout.ActualWidth, flyout.DesiredSize.Width);
        }

        return Math.Max(width, 1d);
    }

    private static double GetOriginalFlyoutHeight(FrameworkElement flyout)
    {
        double height = flyout.Height;
        if (double.IsNaN(height) || height <= 0)
        {
            height = Math.Max(flyout.ActualHeight, flyout.DesiredSize.Height);
        }

        return Math.Max(height, 1d);
    }

    private static double GetFlyoutScale(double width, double height, double layerWidth, double layerHeight)
    {
        double availableWidth = Math.Max(1d, layerWidth - 24d);
        double availableHeight = Math.Max(1d, layerHeight - 24d);
        double scale = Math.Min(1d, Math.Min(availableWidth / width, availableHeight / height));
        return double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0 ? 1d : scale;
    }

    private void MainWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        if (ShouldCloseFloatingPanels(source))
        {
            CloseFloatingPanels();
        }
    }

    private bool ShouldCloseFloatingPanels(DependencyObject? source)
    {
        if (source == null)
        {
            return false;
        }

        if (IsComboBoxPopupInteraction(source))
        {
            return false;
        }

        (FrameworkElement Flyout, FrameworkElement Target)[] items =
        [
            (RoutineIntervalFlyout, RoutineIntervalStatusButton),
            (RecordFormatFlyout, RecordFormatStatusText),
            (AutoShutdownTimeFlyout, AutoShutdownTimeStatusButton),
            (AddRoomFlyout, AddRoomFlyout),
            (LocalSettingsFlyout, LocalSettingsButton),
            (AboutFlyout, AboutFlyout),
            (ExitStrategyFlyout, ExitStrategyFlyout),
            (AutoShutdownPromptFlyout, AutoShutdownPromptFlyout),
        ];

        foreach ((FrameworkElement flyout, FrameworkElement target) in items)
        {
            if (flyout.Visibility == Visibility.Visible &&
                (IsVisualAncestor(source, flyout) || IsVisualAncestor(source, target)))
            {
                return false;
            }
        }

        return items.Any(item => item.Flyout.Visibility == Visibility.Visible);
    }

    private static bool IsComboBoxPopupInteraction(DependencyObject source)
    {
        DependencyObject? current = source;

        while (current != null)
        {
            if (current is System.Windows.Controls.ComboBox or ComboBoxItem)
            {
                return true;
            }

            string typeName = current.GetType().Name;
            if (typeName is "PopupRoot" or "Popup" or "NonLogicalAdornerDecorator")
            {
                return true;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void SetRoutineIntervalFlyoutValue(int milliseconds)
    {
        routineIntervalUnitIndex = RoutineIntervalUnitHelper.GetPreferredUnitIndex(milliseconds);
        double displayValue = RoutineIntervalUnitHelper.ToDisplayValue(milliseconds, routineIntervalUnitIndex);

        isUpdatingRoutineIntervalFlyout = true;
        try
        {
            RoutineIntervalUnitComboBox.SelectedIndex = routineIntervalUnitIndex;
            RoutineIntervalInput.Text = displayValue.ToString("0.###", CultureInfo.InvariantCulture);
        }
        finally
        {
            isUpdatingRoutineIntervalFlyout = false;
        }
    }

    private void SetAutoShutdownTimeFlyoutValue(string value)
    {
        string[] parts = (value ?? string.Empty).Split(':');
        int hour = parts.Length > 0 ? ParseClampedInteger(parts[0], 0, 23) : 0;
        int minute = parts.Length > 1 ? ParseClampedInteger(parts[1], 0, 59) : 0;

        AutoShutdownHourInput.Text = hour.ToString("D2", CultureInfo.InvariantCulture);
        AutoShutdownMinuteInput.Text = minute.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static int ParseClampedInteger(string? value, int min, int max)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Clamp(parsed, min, max)
            : min;
    }

    private void CloseFloatingPanels(FrameworkElement? except = null)
    {
        foreach (FrameworkElement flyout in new[] { RoutineIntervalFlyout, RecordFormatFlyout, AutoShutdownTimeFlyout, AddRoomFlyout, LocalSettingsFlyout, AboutFlyout, ExitStrategyFlyout, AutoShutdownPromptFlyout })
        {
            if (!ReferenceEquals(flyout, except))
            {
                flyout.Visibility = Visibility.Collapsed;
                flyout.LayoutTransform = null;
            }
        }

        if (!ReferenceEquals(except, ExitStrategyFlyout) && ExitStrategyFlyout.Visibility != Visibility.Visible)
        {
            isExitStrategyPending = false;
        }

        UpdateModalOverlay();
    }

    private void UpdateModalOverlay()
    {
        bool modalVisible = AddRoomFlyout.Visibility == Visibility.Visible ||
            LocalSettingsFlyout.Visibility == Visibility.Visible ||
            AboutFlyout.Visibility == Visibility.Visible ||
            ExitStrategyFlyout.Visibility == Visibility.Visible ||
            AutoShutdownPromptFlyout.Visibility == Visibility.Visible;

        ModalOverlay.Visibility = modalVisible ? Visibility.Visible : Visibility.Collapsed;
        MainContentRoot.Effect = modalVisible ? modalBlurEffect : null;
    }

    private void MainWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            CloseFloatingPanels();
            return;
        }

        Dispatcher.BeginInvoke(UpdateVisibleCenteredFlyouts, DispatcherPriority.Loaded);
    }

    private void MainWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateVisibleCenteredFlyouts, DispatcherPriority.Loaded);
    }

    private void UpdateVisibleCenteredFlyouts()
    {
        foreach (FrameworkElement flyout in new[] { AddRoomFlyout, LocalSettingsFlyout, AboutFlyout, ExitStrategyFlyout, AutoShutdownPromptFlyout })
        {
            if (flyout.Visibility == Visibility.Visible)
            {
                CenterVisibleFlyout(flyout);
            }
        }
    }

    private void CancelAutoShutdownPromptClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelAutoShutdownFromPrompt();
        AutoShutdownPromptFlyout.Visibility = Visibility.Collapsed;
        UpdateModalOverlay();
    }

    private void ShutdownNowPromptClick(object sender, RoutedEventArgs e)
    {
        AutoShutdownPromptFlyout.Visibility = Visibility.Collapsed;
        UpdateModalOverlay();
        ViewModel.ShutdownNowFromPrompt();
    }

    private void ShutdownAfterTranscodePromptClick(object sender, RoutedEventArgs e)
    {
        AutoShutdownPromptFlyout.Visibility = Visibility.Collapsed;
        UpdateModalOverlay();
        ViewModel.ShutdownAfterTranscodeFromPrompt();
    }

    private void CloseAutoShutdownPromptClick(object sender, RoutedEventArgs e)
    {
        AutoShutdownPromptFlyout.Visibility = Visibility.Collapsed;
        UpdateModalOverlay();
    }

    private static Point GetRelativePosition(FrameworkElement element, FrameworkElement relativeTo)
    {
        Point elementPoint = element.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));
        Point layerPoint = relativeTo.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));
        return new Point(elementPoint.X - layerPoint.X, elementPoint.Y - layerPoint.Y);
    }

    private static bool IsVisualAncestor(DependencyObject source, DependencyObject ancestor)
    {
        DependencyObject? current = source;

        while (current != null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void RoomCardListLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (isRoomCardDragging)
        {
            FinishRoomCardDrag(false);
        }
    }

    private void StartRoomCardBlankPress(Point position)
    {
        roomCardBlankPressCandidate = true;
        roomCardBlankPressStart = position;
        roomCardBlankPressTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RoomCardBlankLongPressMilliseconds) };
        roomCardBlankPressTimer.Tick -= RoomCardBlankPressTimerTick;
        roomCardBlankPressTimer.Tick += RoomCardBlankPressTimerTick;
        roomCardBlankPressTimer.Stop();
        roomCardBlankPressTimer.Start();
    }

    private void RoomCardBlankPressTimerTick(object? sender, EventArgs e)
    {
        CancelRoomCardBlankPress();

        if (Mouse.LeftButton != MouseButtonState.Pressed || FindRoomCardItemAt(Mouse.GetPosition(RoomCardList)) != null)
        {
            return;
        }

        if (ViewModel.ToggleCardEditModeCommand.CanExecute(null))
        {
            ViewModel.ToggleCardEditModeCommand.Execute(null);
        }
    }

    private void CancelRoomCardBlankPress()
    {
        roomCardBlankPressCandidate = false;
        roomCardBlankPressTimer?.Stop();
    }

    private ListBoxItem? FindRoomCardItemAt(Point position)
    {
        return FindVisualParent<ListBoxItem>(RoomCardList.InputHitTest(position) as DependencyObject);
    }

    private void StartRoomCardDrag(Point position)
    {
        if (draggedRoomItem == null)
        {
            return;
        }

        isRoomCardDragging = true;
        roomCardInsertionIndex = RoomCardList.Items.IndexOf(draggedRoom);
        roomCardAdornerLayer = AdornerLayer.GetAdornerLayer(RoomCardList);

        if (roomCardAdornerLayer != null)
        {
            Size dragSize = new(draggedRoomItem.ActualWidth, draggedRoomItem.ActualHeight);
            roomCardDragAdorner = new DragPreviewAdorner(RoomCardList, CreateRoomCardDragBrush(draggedRoomItem), dragSize);
            roomCardInsertionAdorner = new InsertionLineAdorner(RoomCardList);
            roomCardAdornerLayer.Add(roomCardDragAdorner);
            roomCardAdornerLayer.Add(roomCardInsertionAdorner);
        }

        draggedRoomItem.Opacity = 0;
        draggedRoomItem.IsHitTestVisible = false;
        RoomCardList.CaptureMouse();
        UpdateRoomCardDrag(position);
    }

    private void UpdateRoomCardDrag(Point position)
    {
        roomCardDragAdorner?.Move(position.X - roomCardDragOffset.X, position.Y - roomCardDragOffset.Y);
        (int index, Rect line) = GetRoomCardInsertionPreview(position);
        roomCardInsertionIndex = index;
        roomCardInsertionAdorner?.Update(line);
    }

    private (int Index, Rect Line) GetRoomCardInsertionPreview(Point position)
    {
        int count = RoomCardList.Items.Count;
        int bestIndex = Math.Max(0, count);
        Rect bestLine = Rect.Empty;
        double bestScore = double.MaxValue;

        for (int index = 0; index < count; index++)
        {
            if (RoomCardList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem item || item == draggedRoomItem)
            {
                continue;
            }

            Rect bounds = GetElementBounds(item, RoomCardList);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            bool before = position.X < bounds.Left + bounds.Width / 2d;
            double edgeX = before ? bounds.Left : bounds.Right;
            double dy = position.Y < bounds.Top ? bounds.Top - position.Y : position.Y > bounds.Bottom ? position.Y - bounds.Bottom : 0d;
            double dx = Math.Abs(position.X - edgeX);
            double score = dy * 4d + dx;

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestIndex = before ? index : index + 1;
            double lineTop = bounds.Top + Math.Min(10d, bounds.Height / 5d);
            double lineHeight = Math.Max(28d, bounds.Height - Math.Min(20d, bounds.Height / 2d));
            bestLine = new Rect(edgeX - 1.5d, lineTop, 3d, lineHeight);
        }

        if (bestLine.IsEmpty && draggedRoomItem != null)
        {
            Rect bounds = GetElementBounds(draggedRoomItem, RoomCardList);
            bestIndex = RoomCardList.Items.IndexOf(draggedRoom);
            bestLine = new Rect(bounds.Left - 1.5d, bounds.Top + 8d, 3d, Math.Max(28d, bounds.Height - 16d));
        }

        return (bestIndex, bestLine);
    }

    private static Rect GetElementBounds(FrameworkElement element, Visual relativeTo)
    {
        return element.TransformToVisual(relativeTo).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }

    private void FinishRoomCardDrag(bool commit)
    {
        if (commit && draggedRoom != null && roomCardInsertionIndex >= 0)
        {
            int oldIndex = RoomCardList.Items.IndexOf(draggedRoom);
            int newIndex = roomCardInsertionIndex;

            if (oldIndex >= 0 && oldIndex < newIndex)
            {
                newIndex--;
            }

            ViewModel.MoveRoom(draggedRoom, newIndex);
        }

        ClearRoomCardDrag();
    }

    private void ClearRoomCardDrag()
    {
        isRoomCardDragging = false;

        if (draggedRoomItem != null)
        {
            draggedRoomItem.ClearValue(OpacityProperty);
            draggedRoomItem.ClearValue(IsHitTestVisibleProperty);
        }

        if (roomCardAdornerLayer != null)
        {
            if (roomCardDragAdorner != null)
            {
                roomCardAdornerLayer.Remove(roomCardDragAdorner);
            }

            if (roomCardInsertionAdorner != null)
            {
                roomCardAdornerLayer.Remove(roomCardInsertionAdorner);
            }
        }

        if (RoomCardList.IsMouseCaptured)
        {
            RoomCardList.ReleaseMouseCapture();
        }

        roomCardAdornerLayer = null;
        roomCardDragAdorner = null;
        roomCardInsertionAdorner = null;
        roomCardInsertionIndex = -1;
        draggedRoom = null;
        draggedRoomItem = null;
    }

    private static Brush CreateRoomCardDragBrush(FrameworkElement element)
    {
        double width = Math.Max(1d, element.ActualWidth);
        double height = Math.Max(1d, element.ActualHeight);
        DpiScale dpi = VisualTreeHelper.GetDpi(element);
        RenderTargetBitmap bitmap = new(
            Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY)),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        DrawingVisual visual = new();

        using (DrawingContext drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
        }

        bitmap.Render(visual);
        ImageBrush brush = new(bitmap) { Stretch = Stretch.Fill };
        return brush;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T element && element.Name == name)
            {
                return element;
            }

            T? nested = FindVisualChild<T>(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!TrayIconManager.GetInstance().IsShutdownTriggered)
        {
            e.Cancel = true;
            HandleCloseButtonRequest();
        }
        else
        {
            if (Configurations.IsUseKeepAwake.Get())
            {
                // Stop keep awake
                _ = Kernel32.SetThreadExecutionState(Kernel32.EXECUTION_STATE.ES_CONTINUOUS);
            }
        }
    }

    private void HandleCloseButtonRequest()
    {
        if (Configurations.IsCloseActionRemembered.Get())
        {
            ExecuteCloseAction(Math.Clamp(Configurations.CloseAction.Get(), 0, 2));
            return;
        }

        if (isExitStrategyPending)
        {
            return;
        }

        isExitStrategyPending = true;
        ExitStrategyRememberCheckBox.IsChecked = false;
        OpenCenteredFlyout(ExitStrategyFlyout);
    }

    private void MinimizeToTrayExitClick(object sender, RoutedEventArgs e)
    {
        SaveCloseActionIfNeeded(0);
        ExecuteCloseAction(0);
    }

    private void DirectExitClick(object sender, RoutedEventArgs e)
    {
        SaveCloseActionIfNeeded(1);
        ExecuteCloseAction(1);
    }

    private void CancelExitClick(object sender, RoutedEventArgs e)
    {
        SaveCloseActionIfNeeded(2);
        ExecuteCloseAction(2);
    }

    private void SaveCloseActionIfNeeded(int action)
    {
        if (ExitStrategyRememberCheckBox.IsChecked != true)
        {
            return;
        }

        Configurations.CloseAction.Set(action);
        Configurations.IsCloseActionRemembered.Set(true);
        ConfigurationManager.Save();
    }

    private void ExecuteCloseAction(int action)
    {
        isExitStrategyPending = false;
        CloseFloatingPanels();

        switch (action)
        {
            case 0:
                Hide();
                break;
            case 1:
                TrayIconManager.GetInstance().RequestShutdown();
                break;
        }
    }

    private sealed class DragPreviewAdorner(UIElement adornedElement, Brush brush, Size size) : Adorner(adornedElement)
    {
        private readonly Brush brush = brush;
        private readonly Size size = size;
        private double left;
        private double top;

        public void Move(double x, double y)
        {
            left = x;
            top = y;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.PushOpacity(0.86);
            drawingContext.DrawRoundedRectangle(brush, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 120, 212)), 1), new Rect(left, top, size.Width, size.Height), 8, 8);
            drawingContext.Pop();
        }
    }

    private sealed class InsertionLineAdorner(UIElement adornedElement) : Adorner(adornedElement)
    {
        private Rect line = Rect.Empty;
        private readonly Brush brush = new SolidColorBrush(Color.FromRgb(0, 120, 212));

        public void Update(Rect rect)
        {
            line = rect;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (line.IsEmpty)
            {
                return;
            }

            drawingContext.DrawRoundedRectangle(brush, null, line, 2, 2);
        }
    }
}

