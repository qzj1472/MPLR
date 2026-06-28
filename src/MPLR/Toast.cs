using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MPLR.Core;
using Wpf.Ui.Violeta.Controls;
using Wpf.Ui.Violeta.Threading;
using MediaBrushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace MPLR;

public static class Toast
{
    private static ToastHostWindow? host;

    public static void Information(string message) => Show(message, ToastIcon.Information);

    public static void Success(string message) => Show(message, ToastIcon.Success);

    public static void Warning(string message) => Show(message, ToastIcon.Warning);

    public static void Error(string message) => Show(message, ToastIcon.Error);

    public static void Question(string message) => Show(message, ToastIcon.Question);

    private static void Show(string message, ToastIcon icon)
    {
        AppSessionLogger.Event(GetLevel(icon), "result", "toast", message, new
        {
            icon = icon.ToString(),
        });

        ApplicationDispatcher.Invoke(() =>
        {
            Window? owner = Application.Current.MainWindow;
            if (owner == null)
            {
                return;
            }

            host ??= new ToastHostWindow(owner);
            host.ShowToast(message, icon);
        });
    }

    private static string GetLevel(ToastIcon icon)
    {
        return icon.ToString() switch
        {
            "Error" => "error",
            "Warning" => "warn",
            _ => "info",
        };
    }
}

internal sealed class ToastHostWindow : Window
{
    private readonly Window owner;
    private readonly Grid root = new()
    {
        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
    };
    private readonly DispatcherTimer hideTimer = new();
    private Border? currentToast;

    public ToastHostWindow(Window owner)
    {
        this.owner = owner;
        Owner = owner;
        Width = 720;
        Height = 180;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = root;
        hideTimer.Interval = TimeSpan.FromSeconds(3);
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            HideCurrentToast();
        };

        owner.LocationChanged += (_, _) => UpdatePosition();
        owner.SizeChanged += (_, _) => UpdatePosition();
        owner.StateChanged += (_, _) => UpdateVisibility();
        Loaded += (_, _) => UpdatePosition();
    }

    public void ShowToast(string message, ToastIcon icon)
    {
        UpdateVisibility();
        UpdatePosition();

        Border toast = CreateToast(message, icon);
        hideTimer.Stop();
        root.Children.Clear();
        currentToast = toast;
        root.Children.Add(toast);

        TransformGroup group = new()
        {
            Children =
            {
                new ScaleTransform(0.98, 0.98),
                new TranslateTransform(0, -18),
            },
        };
        toast.RenderTransformOrigin = new Point(0.5, 0.5);
        toast.RenderTransform = group;
        toast.Opacity = 0;

        IEasingFunction showEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)) { EasingFunction = showEase });
        ((ScaleTransform)group.Children[0]).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)) { EasingFunction = showEase });
        ((ScaleTransform)group.Children[0]).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240)) { EasingFunction = showEase });
        ((TranslateTransform)group.Children[1]).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = showEase });

        hideTimer.Start();
    }

    private Border CreateToast(string message, ToastIcon icon)
    {
        Color accent = icon switch
        {
            ToastIcon.Error => Color.FromRgb(185, 28, 28),
            ToastIcon.Warning => Color.FromRgb(180, 83, 9),
            ToastIcon.Success => Color.FromRgb(22, 101, 52),
            _ => Color.FromRgb(0, 60, 106),
        };

        TextBlock glyph = new()
        {
            Text = icon switch
            {
                ToastIcon.Error => "✕",
                ToastIcon.Warning => "!",
                ToastIcon.Success => "✓",
                ToastIcon.Question => "?",
                _ => "i",
            },
            Width = 24,
            Height = 24,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = MediaBrushes.White,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
        };

        Border glyphHost = new()
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(accent),
            Child = glyph,
        };

        TextBlock text = new()
        {
            Text = message,
            FontSize = 18,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27)),
            MaxWidth = 560,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        grid.Children.Add(glyphHost);
        text.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        return new Border
        {
            MaxWidth = 640,
            MinHeight = 48,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(14, 10, 14, 10),
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(55, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
    }

    private void HideCurrentToast()
    {
        Border? toast = currentToast;
        if (toast == null)
        {
            Hide();
            return;
        }

        IEasingFunction hideEase = new CubicEase { EasingMode = EasingMode.EaseInOut };
        DoubleAnimation opacity = new(0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = hideEase,
        };
        opacity.Completed += (_, _) =>
        {
            if (ReferenceEquals(currentToast, toast))
            {
                root.Children.Clear();
                currentToast = null;
            }
            Hide();
        };

        toast.BeginAnimation(UIElement.OpacityProperty, opacity);
        if (toast.RenderTransform is TransformGroup group &&
            group.Children[0] is ScaleTransform scale &&
            group.Children[1] is TranslateTransform translate)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(260)) { EasingFunction = hideEase });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(260)) { EasingFunction = hideEase });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-10, TimeSpan.FromMilliseconds(260)) { EasingFunction = hideEase });
        }
    }

    private void UpdateVisibility()
    {
        if (owner.WindowState == WindowState.Minimized)
        {
            Hide();
            return;
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    private void UpdatePosition()
    {
        if (owner.WindowState == WindowState.Minimized)
        {
            return;
        }

        Left = owner.Left + Math.Max(0, (owner.ActualWidth - Width) / 2);
        Top = owner.Top + 54;
    }
}
