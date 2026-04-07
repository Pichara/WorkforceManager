using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ElevatorMaintenanceSystem.Infrastructure.Converters;

/// <summary>
/// Converts an empty or null string to a placeholder text
/// </summary>
public class EmptyToPlaceholderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var placeholder = parameter?.ToString() ?? "No selection";
        var stringValue = value?.ToString();

        return string.IsNullOrWhiteSpace(stringValue) ? placeholder : stringValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
