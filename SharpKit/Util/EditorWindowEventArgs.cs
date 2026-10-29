namespace SharpKit.Util;

public sealed class EditorWindowEventArgs<T> where T : class
{
    public bool Result { get; internal set; }
    
    public T? Item { get; internal set; }
    
    public EditorWindowEventArgs() {}

    public EditorWindowEventArgs(bool result, T? item)
    {
        Result = result;
        Item = item;
    }
}