using System.Globalization;

namespace MPLR.Core;

internal static class SegmentTimeUnitHelper
{
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const double BytesPerMegabyte = 1000d * 1000d;
    private const double BytesPerGigabyte = 1000d * 1000d * 1000d;

    public const int Seconds = 0;
    public const int Minutes = 1;
    public const int Hours = 2;
    public const int Megabytes = 3;
    public const int Gigabytes = 4;

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

    public static double ToDisplayValue(int value, int unitIndex)
    {
        return unitIndex switch
        {
            Gigabytes => value / BytesPerGigabyte,
            Megabytes => value / BytesPerMegabyte,
            Hours => value / (double)SecondsPerHour,
            Minutes => value / (double)SecondsPerMinute,
            _ => value,
        };
    }

    public static int ToSeconds(double value, int unitIndex)
    {
        if (IsSizeUnit(unitIndex))
        {
            double sizeMultiplier = unitIndex == Gigabytes ? BytesPerGigabyte : BytesPerMegabyte;
            return (int)Math.Clamp(Math.Round(value * sizeMultiplier, MidpointRounding.AwayFromZero), BytesPerMegabyte, int.MaxValue);
        }

        double timeMultiplier = unitIndex switch
        {
            Hours => SecondsPerHour,
            Minutes => SecondsPerMinute,
            _ => 1,
        };

        return (int)Math.Max(10, Math.Round(value * timeMultiplier, MidpointRounding.AwayFromZero));
    }

    public static string GetUnitText(int unitIndex)
    {
        if (Locale.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return unitIndex switch
            {
                Gigabytes => "GB",
                Megabytes => "MB",
                Hours => "小时",
                Minutes => "分钟",
                _ => "秒",
            };
        }

        return unitIndex switch
        {
            Gigabytes => "GB",
            Megabytes => "MB",
            Hours => "h",
            Minutes => "min",
            _ => "s",
        };
    }

    public static string GetValueLabel(int unitIndex)
    {
        bool isChinese = Locale.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
        if (IsSizeUnit(unitIndex))
        {
            return isChinese ? "分割大小" : "Split size";
        }

        return isChinese ? "分割时长" : "Split duration";
    }

    public static bool IsSizeUnit(int unitIndex)
    {
        return unitIndex is Megabytes or Gigabytes;
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
