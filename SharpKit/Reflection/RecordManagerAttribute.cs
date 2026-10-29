namespace SharpKit.Reflection;

[AttributeUsage(AttributeTargets.Class)]
public class RecordManagerAttribute(Type type) : Attribute
{
    public Type Type => type;
}