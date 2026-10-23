using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpKit.Windows.Data;

public partial class DataFileSelectorWindowVM : ObservableObject
{
    public partial class ModItem : ObservableObject
    {
        [ObservableProperty] public partial string Filename { get; set; }
        [ObservableProperty] public partial string CreatedBy { get; set; }
        [ObservableProperty] public partial string Summary { get; set; }
        [ObservableProperty] public partial ObservableCollection<string> ParentMasters { get; set; }
        [ObservableProperty] public partial DateTime CreatedOn { get; set; }
        [ObservableProperty] public partial DateTime LastModified { get; set; }
        [ObservableProperty] public partial bool IsMaster { get; set; }
        [ObservableProperty] public partial int LoadOrder { get; set; }
        
        [ObservableProperty] public partial bool Selected { get; set; }
    }
    
    [ObservableProperty] public partial ModItem? ActiveItem { get; set; }
    
    [ObservableProperty] public partial ObservableCollection<ModItem> Plugins { get; set; } = [];
    
    [ObservableProperty] public partial ObservableCollection<ModItem> FilteredPlugins { get; set; } = [];
    
    [ObservableProperty] public partial ModItem? SelectedItem { get; set; }

    [ObservableProperty] public partial string SearchString { get; set; } = string.Empty;
}