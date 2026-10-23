using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpKit.Windows.Data;

public partial class FormExplorerVM : ObservableObject
{
    public List<TreeItem> Items { get; set; } = [];

    [ObservableProperty] public partial string Search { get; set; } = "";

    [ObservableProperty] public partial ObservableCollection<TreeRowItem> Rows { get; set; } = [];
    
    [ObservableProperty] public partial ObservableCollection<TreeRowItem> FilteredRows { get; set; } = [];
}