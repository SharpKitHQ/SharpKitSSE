using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Windows.Data.FormEditors;

public partial class CellExplorerVM : ObservableObject
{
    public record WorldspaceContainer(IWorldspaceGetter? Space, string Name);
    
    [ObservableProperty] public partial ObservableCollection<WorldspaceContainer> Worldspaces { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<ICellGetter> InteriorCells { get; set; } = [];
    
    [ObservableProperty] public partial WorldspaceContainer? SelectedWorldspace { get; set; }

    [ObservableProperty] public partial ObservableCollection<ICellGetter> SelectedCells { get; set; } = [];
}