using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpKit.Windows;

public partial class MainWindowVM : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = "SharpKit";

    public ICommand OpenDataWindow { get; set; }
}