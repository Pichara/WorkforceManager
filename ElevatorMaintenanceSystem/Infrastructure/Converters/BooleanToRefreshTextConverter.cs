using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ElevatorMaintenanceSystem.Infrastructure.Converters;

/// <summary>
/// Converts a boolean IsBusy value to "Refresh Map" or "Refreshing..." text
/// </summary>
public class BooleanToRefreshTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBusy && isBusy)
        {
            return "Refreshing...";
        }
        return "Refresh Map";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
