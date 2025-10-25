using System;
using System.Globalization;
using System.Windows.Data;

namespace SteamPersonaSwitcher;

/// <summary>
/// Converter that returns true if a string is not null or whitespace
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
