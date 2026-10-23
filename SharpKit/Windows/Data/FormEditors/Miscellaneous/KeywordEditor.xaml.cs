using System.Windows;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;

namespace SharpKit.Windows.Data.FormEditors.Miscellaneous;

[RecordEditor(typeof(Keyword))]
public partial class KeywordEditor : Window
{
    public string ID { get; set; } = "";
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
    
    
    public KeywordEditor(IKeywordGetter keyword)
    {
        InitializeComponent();

        ID = keyword.EditorID ?? "";
        R = keyword.Color?.R ?? 0;
        G = keyword.Color?.G ?? 0;
        B = keyword.Color?.B ?? 0;
        
        DataContext = this;
    }
}