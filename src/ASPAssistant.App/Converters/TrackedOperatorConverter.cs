using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ASPAssistant.Core.Models;

namespace ASPAssistant.App.Converters;

public sealed class TrackedOperatorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not TrackingEntry entry || values[1] is not IReadOnlyDictionary<string, Operator> operators)
            return DependencyProperty.UnsetValue;
        return operators.TryGetValue(entry.Name, out var op) ? op : entry;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
