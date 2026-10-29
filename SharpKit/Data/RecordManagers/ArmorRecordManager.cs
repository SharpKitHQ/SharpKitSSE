using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;
using SharpKit.Util;

namespace SharpKit.Data.RecordManagers;

[RecordManager(typeof(IArmor))]
public class ArmorRecordManager(PluginFileManager pfm, EditorProvider ep) : IRecordManager
{
    public void ShowForEdit(object recordInstance)
    {
        if(recordInstance is not IArmorGetter k)
            throw new ArgumentException("Provided object does not implement IArmorGetter", nameof(recordInstance));

        var editor = ep.GetEditorWindow(k);
        if (editor is null)
            throw new Exception("No editor found for IArmorGetter");

        editor.Closed += OnEditorClosed;
        editor.Show();
    }

    public void ShowForNew()
    {
        var editor = ep.GetEditorWindow<IArmorGetter>();
        if (editor is null)
            throw new Exception("No editor found for IArmorGetter");

        editor.Closed += OnEditorClosed;
        editor.Show();
    }
    
    private void OnEditorClosed(object? sender, EditorWindowEventArgs<IArmorGetter> args)
    {
        if (!args.Result) 
            return;

        var item = args.Item;
        if (item is null)
            return;

        var existing = pfm.GetActiveMod()?.Armors.GetOrAddAsOverride(item);

        if (existing is null) return;
        
        
        
    }
}