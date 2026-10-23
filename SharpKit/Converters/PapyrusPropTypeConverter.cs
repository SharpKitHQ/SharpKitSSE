using System.Globalization;
using System.Windows.Data;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Converters;

public class PapyrusPropTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IScriptPropertyGetter prop) return null;

        return prop switch
        {
            ScriptBoolListProperty => "Bool[]",
            ScriptBoolProperty => "Bool",
            ScriptFloatListProperty => "Float[]",
            ScriptFloatProperty => "Float",
            ScriptIntListProperty => "Int[]",
            ScriptIntProperty _ => "Int",
            ScriptObjectListProperty => "Object[]",
            ScriptObjectProperty => "Object",
            ScriptStringListProperty => "String[]",
            ScriptStringProperty => "String",
            ScriptProperty => "Script",
            _ => "Unknown"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}