using System.Windows;

namespace SharpKit.Util;

public interface IRecordEditorWindow<T> : IRecordEditorWindow where T : class
{
    public delegate void ClosedEvent(object? sender, EditorWindowEventArgs<T> args);

    public event ClosedEvent? Closed;
}

public interface IRecordEditorWindow
{
    public void Show(Window? owner = null);
}