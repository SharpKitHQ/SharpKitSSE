using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SharpKit.Papyrus;

public class PapyrusCompletionData(string keyword) : ICompletionData
{
    public ImageSource? Image => null;
    public string Text => keyword;
    public object Content => keyword;
    public object Description => $"Papyrus keyword: {keyword}";
    public double Priority => 0;
    
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}