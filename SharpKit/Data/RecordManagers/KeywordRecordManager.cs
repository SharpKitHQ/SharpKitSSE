using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;
using SharpKit.Util;

namespace SharpKit.Data.RecordManagers;

[RecordManager(typeof(IKeyword))]
public class KeywordRecordManager(PluginFileManager pfm, EditorProvider ep) : IRecordManager
{
    public void ShowForEdit(object recordInstance)
    {
        if(recordInstance is not IKeywordGetter k)
            throw new ArgumentException("Provided object does not implement IKeywordGetter", nameof(recordInstance));

        var editor = ep.GetEditorWindow(k);
        if (editor is null)
            throw new Exception("No editor found for IKeywordGetter");

        editor.Closed += OnEditorClosed;
        editor.Show();
    }

    public void ShowForNew()
    {
        var editor = ep.GetEditorWindow<IKeywordGetter>();
        if (editor is null)
            throw new Exception("No editor found for IKeywordGetter");

        editor.Closed += OnEditorClosed;
        editor.Show();
    }

    private void OnEditorClosed(object? sender, EditorWindowEventArgs<IKeywordGetter> args)
    {
        if (!args.Result) 
            return;

        var item = args.Item;
        if (item is null)
            return;

        var existing = pfm.GetActiveMod()?.Keywords.GetOrAddAsOverride(item);

        if (existing is null) return;
        existing.EditorID = item.EditorID;
        existing.Color = item.Color;
        
        
    }
}