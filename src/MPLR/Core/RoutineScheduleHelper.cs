namespace MPLR.Core;

internal static class RoutineScheduleHelper
{
    public static bool IsActive(DateTime now)
    {
        if (Configurations.RoutineScheduleMode.Get() != 1)
        {
            return true;
        }

        HashSet<int> days = ParseDays(Configurations.RoutineScheduleDays.Get());

        if (!days.Contains((int)now.DayOfWeek))
        {
            return false;
        }

        TimeSpan current = now.TimeOfDay;
        TimeSpan start = new(
            Math.Clamp(Configurations.RoutineScheduleStartHour.Get(), 0, 23),
            Math.Clamp(Configurations.RoutineScheduleStartMinute.Get(), 0, 59),
            0);
        TimeSpan end = new(
            Math.Clamp(Configurations.RoutineScheduleEndHour.Get(), 0, 23),
            Math.Clamp(Configurations.RoutineScheduleEndMinute.Get(), 0, 59),
            59);

        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }

    private static HashSet<int> ParseDays(string value)
    {
        HashSet<int> days = [];

        foreach (string item in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(item, out int day) && day >= 0 && day <= 6)
            {
                days.Add(day);
            }
        }

        return days;
    }
}
