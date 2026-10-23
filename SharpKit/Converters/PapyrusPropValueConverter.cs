using System.Globalization;
using System.Windows.Data;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Converters;

public class PapyrusPropValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IScriptPropertyGetter prop) return null;

        return prop switch
        {
            ScriptBoolListProperty x => $"[{string.Join(", ", x.Data)}]",
            ScriptBoolProperty x => x.Data.ToString(),
            ScriptFloatListProperty x => $"[{string.Join(", ", x.Data)}]",
            ScriptFloatProperty x => $"{x.Data:F2}",
            ScriptIntListProperty x => $"[{string.Join(", ", x.Data)}]",
            ScriptIntProperty x => x.Data.ToString(),
            ScriptObjectListProperty x => $"[{string.Join(", ", x.Objects)}]",
            ScriptObjectProperty x => x.Object.ToString(),
            ScriptStringListProperty x => $"[{string.Join(", ", x.Data)}]",
            ScriptStringProperty x => x.Data,
            ScriptProperty x => x.Name,
            _ => "Unknown"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}