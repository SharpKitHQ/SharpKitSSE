using System.Windows;

namespace SharpKit.Util;

public class RecordEditorWindow<T> : Window, IRecordEditorWindow<T> where T : class
{
    internal bool ActionResult { get; set; }
    
    internal T? Item { get; set; }
    
    public void Show(Window? owner = null)
    {
        ShowInTaskbar = false;
        if (owner is not null) Owner = owner;
        base.Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        Closed?.Invoke(this, new EditorWindowEventArgs<T>(ActionResult, Item));
        base.OnClosed(e);
    }

    public new event IRecordEditorWindow<T>.ClosedEvent? Closed;
}