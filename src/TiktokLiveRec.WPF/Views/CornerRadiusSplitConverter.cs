using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TiktokLiveRec.Views;

public sealed class CornerRadiusSplitConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        CornerRadius original = new(0);

        if (values.Length > 0 && values[0] is CornerRadius cornerRadius)
        {
            original = cornerRadius;
        }

        bool isExpanded = values.Length > 1 && values[1] is true;
        string side = parameter as string ?? "Top";

        if (string.Equals(side, "Top", StringComparison.OrdinalIgnoreCase))
        {
            return isExpanded ? new CornerRadius(original.TopLeft, original.TopRight, 0, 0) : original;
        }

        return isExpanded ? new CornerRadius(0, 0, original.BottomRight, original.BottomLeft) : new CornerRadius(0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
