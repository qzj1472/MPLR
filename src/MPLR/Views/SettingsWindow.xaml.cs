using MPLR.ViewModels;
using MPLR.Core;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace MPLR.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsViewModel ViewModel { get; }
    private readonly BlurEffect modalBlurEffect = new() { Radius = 8 };

    public SettingsWindow()
    {
        DataContext = ViewModel = new();
        WindowSizing.UseRelativeMainWindowSize(this, 700d, 560d);
        InitializeComponent();
        SizeChanged += SettingsWindowSizeChanged;
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

    private void OpenLogExportFlyoutClick(object sender, RoutedEventArgs e)
    {
        OpenCenteredFlyout(LogExportFlyout);
    }

    private void CloseLogExportFlyoutClick(object sender, RoutedEventArgs e)
    {
        CloseFloatingPanels();
    }

    private void SettingsModalOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        CloseFloatingPanels();
        e.Handled = true;
    }

    private void ExportLatestLogsClick(object sender, RoutedEventArgs e)
    {
        CloseFloatingPanels();
        ViewModel.ExportLogs(latest: true);
    }

    private void ExportAllLogsClick(object sender, RoutedEventArgs e)
    {
        CloseFloatingPanels();
        ViewModel.ExportLogs(latest: false);
    }

    private void OpenCenteredFlyout(FrameworkElement flyout)
    {
        CloseFloatingPanels(flyout);
        flyout.Visibility = Visibility.Visible;
        UpdateModalOverlay();
        Dispatcher.BeginInvoke(() => CenterVisibleFlyout(flyout), DispatcherPriority.Loaded);
    }

    private void CenterVisibleFlyout(FrameworkElement flyout)
    {
        if (flyout.Visibility != Visibility.Visible)
        {
            return;
        }

        flyout.UpdateLayout();
        SettingsFlyoutLayer.UpdateLayout();

        double originalWidth = GetOriginalFlyoutWidth(flyout);
        double originalHeight = GetOriginalFlyoutHeight(flyout);
        double layerWidth = SettingsFlyoutLayer.ActualWidth > 1 ? SettingsFlyoutLayer.ActualWidth : Math.Max(ActualWidth, 1);
        double layerHeight = SettingsFlyoutLayer.ActualHeight > 1 ? SettingsFlyoutLayer.ActualHeight : Math.Max(ActualHeight, 1);
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

    private void CloseFloatingPanels(FrameworkElement? except = null)
    {
        foreach (FrameworkElement flyout in new[] { LogExportFlyout })
        {
            if (!ReferenceEquals(flyout, except))
            {
                flyout.Visibility = Visibility.Collapsed;
                flyout.LayoutTransform = null;
            }
        }

        UpdateModalOverlay();
    }

    private void UpdateModalOverlay()
    {
        bool modalVisible = LogExportFlyout.Visibility == Visibility.Visible;
        SettingsModalOverlay.Visibility = modalVisible ? Visibility.Visible : Visibility.Collapsed;
        SettingsContentRoot.Effect = modalVisible ? modalBlurEffect : null;
    }

    private void SettingsWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (LogExportFlyout.Visibility == Visibility.Visible)
            {
                CenterVisibleFlyout(LogExportFlyout);
            }
        }, DispatcherPriority.Loaded);
    }
}
