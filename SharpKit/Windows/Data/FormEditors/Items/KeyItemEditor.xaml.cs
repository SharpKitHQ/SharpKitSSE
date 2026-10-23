using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Serilog;
using SharpKit.Reflection;
using SharpKit.Windows.Data.FormEditors.Other;

namespace SharpKit.Windows.Data.FormEditors.Items;

[RecordEditor(typeof(Key))]
public partial class KeyItemEditor : Window
{
    public string? EditorID { get; set; }
    
    public string? KeyName { get; set; }
    
    public float Weight { get; set; }
    
    public uint Value { get; set; }
    
    public bool Playable { get; set; }
    
    public string? Model { get; set; }
    
    public List<IKeywordGetter> Keywords { get; set; }
    
    public IReadOnlyList<IScriptEntryGetter> Scripts { get; set; }
    
    public KeyItemEditor(IKeyGetter key)
    {
        InitializeComponent();

        var pluginManager = App.ServiceProvider?.GetRequiredService<PluginFileManager>() ??
                            throw new Exception("App.ServiceProvider is null");

        var env = pluginManager.GetEnv();
        
        EditorID = key.EditorID;
        KeyName = key.Name.String;
        Weight = key.Weight;
        Value = key.Value;
        Playable = !key.MajorFlags.HasFlag(Key.MajorFlag.NonPlayable);
        Model = key.Model?.File.GivenPath;
        Keywords = [.. key.Keywords?.Select(k => k.Resolve(env.LinkCache)) ?? []];
        Scripts = key.VirtualMachineAdapter?.Scripts ?? [];

        DataContext = this;
        
        
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // private void Scripts_EditSource_OnClick(object sender, RoutedEventArgs e)
    // {
    //     if (ScriptList.SelectedItem is not IScriptEntryGetter script) return;
    //
    //     var editor = new ScriptEditor(script);
    //     editor.ShowDialog();
    // }
    //
    // private void ScriptPropsButton_OnClick(object sender, RoutedEventArgs e)
    // {
    //     if (ScriptList.SelectedItem is not IScriptEntryGetter script) return;
    //
    //     var editor = new ScriptPropsEditor(script);
    //     editor.ShowDialog();
    // }
}