using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Windows.Data;

public class TreeItem
{
    public TreeItem() {}

    public TreeItem(string name, Type? recordType, List<TreeItem>? children = null, Func<IEnumerable<ISkyrimModGetter>, IEnumerable<ISkyrimMajorRecordGetter>>? recordGetter = null)
    {
        Name = name;
        RecordType = recordType;
        Children = children ?? [];
        GetRecords = recordGetter;
    }
    
    public string Name { get; set; }

    public List<TreeItem> Children { get; set; } = [];
    
    public Type? RecordType { get; set; }
    
    public Func<IEnumerable<ISkyrimModGetter>, IEnumerable<ISkyrimMajorRecordGetter>>? GetRecords { get; set; }
}