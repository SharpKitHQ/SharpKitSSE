using System.Windows;

namespace SharpKit.Util;

public class DialogWindow : Window
{
    protected bool IsDialog { get; set; }
    
    public new bool? ShowDialog()
    {
        IsDialog = true;
        return base.ShowDialog();
    }

    public new bool? DialogResult
    {
        get => base.DialogResult;
        set
        {
            if (IsDialog) base.DialogResult = value;
            field = value;
        }
    }
}