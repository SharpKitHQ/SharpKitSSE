using System.Globalization;
using System.Windows.Data;
using Mutagen.Bethesda.Strings;

namespace SharpKit.Converters;

public class TranslatedStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not ITranslatedStringGetter str ? null : str.String;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string str) return null;

        return new TranslatedString(Language.English, str);
    }
}