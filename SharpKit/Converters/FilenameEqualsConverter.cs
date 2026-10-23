using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace SharpKit.Converters;

public class FilenameEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string filename         &&
               parameter is string activeFile   &&
               string.Equals(filename, activeFile, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}