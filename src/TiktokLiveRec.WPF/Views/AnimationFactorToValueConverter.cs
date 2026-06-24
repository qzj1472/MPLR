using System.Globalization;
using System.Windows.Data;

namespace TiktokLiveRec.Views;

public sealed class AnimationFactorToValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double value = values.Length > 0 && values[0] is double currentValue ? currentValue : 0;
        double factor = values.Length > 1 && values[1] is double currentFactor ? currentFactor : 0;
        double result = value * factor;

        return string.Equals(parameter as string, "negative", StringComparison.OrdinalIgnoreCase) ? -result : result;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
