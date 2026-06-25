using System.Windows;
using System.Windows.Media;
using MPLR.Core;
using Wpf.Ui.Violeta.Controls;

namespace MPLR;

public static class Toast
{
    private static readonly Thickness Offset = new(0, 52, 0, 0);

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

        ToastConfig config = new(icon, ToastLocation.TopCenter, Offset, ToastConfig.NormalTime)
        {
            FontSize = 24,
            IconSize = 24,
            FontWeight = FontWeights.Medium,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            OffsetMargin = Offset,
        };

        Wpf.Ui.Violeta.Controls.Toast.Show(Application.Current.MainWindow, message, config);
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

