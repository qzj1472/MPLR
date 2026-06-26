using System.Globalization;

namespace MPLR.Core;

internal static class SegmentTimeUnitHelper
{
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;

    public const int Seconds = 0;
    public const int Minutes = 1;
    public const int Hours = 2;

    public static int GetPreferredUnitIndex(int seconds)
    {
        if (seconds >= SecondsPerHour && seconds % SecondsPerHour == 0)
        {
            return Hours;
        }

        if (seconds >= SecondsPerMinute && seconds % SecondsPerMinute == 0)
        {
            return Minutes;
        }

        return Seconds;
    }

    public static double ToDisplayValue(int seconds, int unitIndex)
    {
        return unitIndex switch
        {
            Hours => seconds / (double)SecondsPerHour,
            Minutes => seconds / (double)SecondsPerMinute,
            _ => seconds,
        };
    }

    public static int ToSeconds(double value, int unitIndex)
    {
        double multiplier = unitIndex switch
        {
            Hours => SecondsPerHour,
            Minutes => SecondsPerMinute,
            _ => 1,
        };

        return (int)Math.Max(10, Math.Round(value * multiplier, MidpointRounding.AwayFromZero));
    }

    public static string GetUnitText(int unitIndex)
    {
        if (Locale.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return unitIndex switch
            {
                Hours => "小时",
                Minutes => "分钟",
                _ => "秒",
            };
        }

        return unitIndex switch
        {
            Hours => "h",
            Minutes => "min",
            _ => "s",
        };
    }

    public static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0001d)
        {
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
