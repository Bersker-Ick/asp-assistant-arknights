using System.Globalization;
using System.Windows.Data;

namespace ASPAssistant.App.Converters;

public sealed class CovenantHighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values[0] is string name && values[1] is IReadOnlySet<string> names && names.Contains(name)
            ? "available" : "";

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
