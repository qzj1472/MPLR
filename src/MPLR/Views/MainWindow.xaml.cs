using Microsoft.Toolkit.Uwp.Notifications;
using CommunityToolkit.Mvvm.Messaging;
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
using MPLR.ViewModels;
using Vanara.PInvoke;
using Wpf.Ui.Controls;
using Brush = System.Windows.Media.Brush;
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

    private readonly BlurEffect modalBlurEffect = new() { Radius = 8 };

    private const double BottomFlyoutGap = 40d;

    public MainWindow()
    {
        DataContext = ViewModel = new();
        InitializeComponent();
        PreviewMouseDown += MainWindowPreviewMouseDown;
        StateChanged += MainWindowStateChanged;
        WeakReferenceMessenger.Default.Register<RoomCardsFlashMessage>(this, (_, _) =>
        {
            Dispatcher.BeginInvoke(BeginRoomCardsFlashAnimation, DispatcherPriority.Background);
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
    }

    private void RecordFormatStatusTextMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenBoundedFlyout(RecordFormatFlyout, RecordFormatStatusText);
        e.Handled = true;
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
        OpenCenteredFlyout(AddRoomFlyout);
        Dispatcher.BeginInvoke(() => AddRoomUrlInput.Focus(), DispatcherPriority.Input);
    }

    private void OpenAboutFlyoutClick(object sender, RoutedEventArgs e)
    {
        OpenCenteredFlyout(AboutFlyout);
    }

    private async void AddRoomConfirmClick(object sender, RoutedEventArgs e)
    {
        bool added = await ViewModel.TryAddRoomFromFlyoutAsync(
            AddRoomUrlInput.Text,
            AddRoomForceCheckBox.IsChecked == true,
            AddRoomNotifyCheckBox.IsChecked == true,
            AddRoomFollowGlobalSettingsCheckBox.IsChecked == true);
        if (added)
        {
            AddRoomFlyout.Visibility = Visibility.Collapsed;
            UpdateModalOverlay();
        }
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

        double flyoutWidth = Math.Max(flyout.ActualWidth, flyout.DesiredSize.Width);
        double flyoutHeight = Math.Max(flyout.ActualHeight, flyout.DesiredSize.Height);
        double layerWidth = MainFlyoutLayer.ActualWidth > 1 ? MainFlyoutLayer.ActualWidth : Math.Max(ActualWidth - MainFlyoutLayer.Margin.Left - MainFlyoutLayer.Margin.Right, 1);
        double layerHeight = MainFlyoutLayer.ActualHeight > 1 ? MainFlyoutLayer.ActualHeight : Math.Max(ActualHeight - MainFlyoutLayer.Margin.Top - MainFlyoutLayer.Margin.Bottom, 1);
        double left = Math.Clamp((layerWidth - flyoutWidth) / 2d, 0, Math.Max(0, layerWidth - flyoutWidth));
        double top = Math.Clamp((layerHeight - flyoutHeight) / 2d, 0, Math.Max(0, layerHeight - flyoutHeight));

        Canvas.SetLeft(flyout, left);
        Canvas.SetTop(flyout, top);
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

        (FrameworkElement Flyout, FrameworkElement Target)[] items =
        [
            (RoutineIntervalFlyout, RoutineIntervalStatusButton),
            (RecordFormatFlyout, RecordFormatStatusText),
            (AddRoomFlyout, AddRoomFlyout),
            (AboutFlyout, AboutFlyout),
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

    private void CloseFloatingPanels(FrameworkElement? except = null)
    {
        foreach (FrameworkElement flyout in new[] { RoutineIntervalFlyout, RecordFormatFlyout, AddRoomFlyout, AboutFlyout })
        {
            if (!ReferenceEquals(flyout, except))
            {
                flyout.Visibility = Visibility.Collapsed;
            }
        }

        UpdateModalOverlay();
    }

    private void UpdateModalOverlay()
    {
        bool modalVisible = AddRoomFlyout.Visibility == Visibility.Visible ||
            AboutFlyout.Visibility == Visibility.Visible;

        ModalOverlay.Visibility = modalVisible ? Visibility.Visible : Visibility.Collapsed;
        MainContentRoot.Effect = modalVisible ? modalBlurEffect : null;
    }

    private void MainWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            CloseFloatingPanels();
        }
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
            Hide();

            if (!Configurations.IsOffRemindCloseToTray.Get())
            {
                Notifier.AddNoticeWithButton("Title".Tr(), "CloseToTrayHint".Tr(), [
                    new ToastContentButtonOption()
                    {
                        Content = "ButtonOfOffRemind".Tr(),
                        Arguments = [("OffRemindTheCloseToTrayHint", bool.TrueString)],
                        ActivationType = ToastActivationType.Background,
                    },
                    new ToastContentButtonOption()
                    {
                        Content = "ButtonOfClose".Tr(),
                        ActivationType = ToastActivationType.Foreground,
                    },
                ]);
            }
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

