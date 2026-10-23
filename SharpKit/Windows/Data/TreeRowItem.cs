using CommunityToolkit.Mvvm.ComponentModel;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Windows.Data;

public partial class TreeRowItem : ObservableObject
{
    public string EditorID { get; set; } = "";
    public uint FormID { get; set; }
    public int Count { get; set; }
    public int Users { get; set; }
    
    public IMajorRecordGetter? Instance { get; set; }
}