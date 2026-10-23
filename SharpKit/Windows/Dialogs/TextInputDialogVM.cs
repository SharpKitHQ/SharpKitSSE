using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpKit.Windows.Dialogs;

public partial class TextInputDialogVM : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = "Enter a Value";
    [ObservableProperty] public partial string Description { get; set; } = "Enter a Value";
    [ObservableProperty] public partial string Input { get; set; } = "";
}