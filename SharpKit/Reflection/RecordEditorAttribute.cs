namespace SharpKit.Reflection;

[AttributeUsage(AttributeTargets.Class)]
public class RecordEditorAttribute(Type recordType) : Attribute
{
    public Type RecordType => recordType;
}