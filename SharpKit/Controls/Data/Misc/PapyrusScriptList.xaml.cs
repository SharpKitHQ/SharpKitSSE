using System.Windows;
using System.Windows.Controls;
using Mutagen.Bethesda.Skyrim;
using UserControl = System.Windows.Controls.UserControl;

namespace SharpKit.Controls.Data.Misc;

public partial class PapyrusScriptList : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IReadOnlyList<IScriptEntryGetter>),
            typeof(PapyrusScriptList),
            new PropertyMetadata(null));
    
    public IReadOnlyList<IScriptEntryGetter>? ItemsSource
    {
        get => (IReadOnlyList<IScriptEntryGetter>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedScriptProperty =
        DependencyProperty.Register(
            nameof(SelectedScript),
            typeof(IScriptEntryGetter),
            typeof(PapyrusScriptList),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public IScriptEntryGetter? SelectedScript
    {
        get => (IScriptEntryGetter?)GetValue(SelectedScriptProperty);
        set => SetValue(SelectedScriptProperty, value);
    }
    
    public PapyrusScriptList()
    {
        InitializeComponent();
    }
    
    private void Scripts_EditSource_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedScript is not { } script)
            return;

        var editor = new SharpKit.Windows.Data.FormEditors.Other.ScriptEditor(script);
        editor.ShowDialog();
    }

    private void ScriptPropsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedScript is not { } script)
            return;

        var editor = new SharpKit.Windows.Data.FormEditors.Other.ScriptPropsEditor(script);
        editor.ShowDialog();
    }
}