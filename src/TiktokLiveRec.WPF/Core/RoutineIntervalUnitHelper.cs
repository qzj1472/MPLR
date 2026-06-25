using System.Globalization;

namespace TiktokLiveRec.Core;

internal static class RoutineIntervalUnitHelper
{
    private const int MillisecondsPerSecond = 1000;
    private const int MillisecondsPerMinute = 60000;

    public const int Seconds = 0;
    public const int Minutes = 1;

    public static int GetPreferredUnitIndex(int milliseconds)
    {
        return milliseconds >= MillisecondsPerMinute ? Minutes : Seconds;
    }

    public static double ToDisplayValue(int milliseconds, int unitIndex)
    {
        return unitIndex == Minutes ? milliseconds / (double)MillisecondsPerMinute : milliseconds / (double)MillisecondsPerSecond;
    }

    public static int ToMilliseconds(double value, int unitIndex)
    {
        double multiplier = unitIndex == Minutes ? MillisecondsPerMinute : MillisecondsPerSecond;
        return (int)Math.Max(500, Math.Round(value * multiplier, MidpointRounding.AwayFromZero));
    }

    public static string FormatDisplayValue(int milliseconds)
    {
        int unitIndex = GetPreferredUnitIndex(milliseconds);
        double value = ToDisplayValue(milliseconds, unitIndex);
        string suffix = GetShortUnitText(unitIndex);

        return $"{FormatNumber(value)}{suffix}";
    }

    public static string GetUnitText(int unitIndex)
    {
        if (Locale.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return unitIndex == Minutes ? "分钟" : "秒";
        }

        return unitIndex == Minutes ? "min" : "s";
    }

    public static string GetShortUnitText(int unitIndex)
    {
        if (Locale.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return unitIndex == Minutes ? "分钟" : "秒";
        }

        return unitIndex == Minutes ? "min" : "s";
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0001d)
        {
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
