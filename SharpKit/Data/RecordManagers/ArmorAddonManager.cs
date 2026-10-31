using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;
using SharpKit.Util;

namespace SharpKit.Data.RecordManagers;

[RecordManager(typeof(IArmorAddon))]
public class ArmorAddonManager(PluginFileManager pfm, EditorProvider ep) : IRecordManager
{
    public void ShowForEdit(object recordInstance)
    {
        if(recordInstance is not IArmorAddonGetter k)
            throw new ArgumentException("Provided object does not implement IArmorAddonGetter", nameof(recordInstance));

        var editor = ep.GetEditorWindow(k);
        if (editor is null)
            throw new Exception("No editor found for IArmorAddonGetter");

        // editor.Closed += OnEditorClosed;
        editor.Show();
    }

    public void ShowForNew()
    {
        throw new NotImplementedException();
    }
}