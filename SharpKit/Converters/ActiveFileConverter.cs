using System.Globalization;
using System.Windows.Data;
using SharpKit.Windows.Data;

namespace SharpKit.Converters;

public class ActiveFileConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not string filename ||
            values[1] is not DataFileSelectorWindowVM.ModItem activeFile)
        {
            return false;
        }

        return string.Equals(
            filename,
            activeFile.Filename,
            StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
        => throw new NotSupportedException();
}