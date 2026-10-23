using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace SharpKit.Reflection;

public class EditorProvider
{
    private readonly ILogger<EditorProvider> _log;
    private readonly Dictionary<Type, Type> _editorTypes;

    public EditorProvider(ILogger<EditorProvider> log)
    {
        _log = log;
        log.LogInformation("Looking for record editors...");
        
        _editorTypes = GetType().Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<RecordEditorAttribute>() != null)
            .Select(t => new KeyValuePair<Type, Type>(t.GetCustomAttribute<RecordEditorAttribute>()?.RecordType ?? throw new ArgumentException("RecordEditorAttribute RecordType property must not be null"), t))
            .ToDictionary();
        
        log.LogInformation("Found {Count} editors:", _editorTypes.Count);
        foreach (var (tr, te) in _editorTypes)
        {
            log.LogInformation(" - {tr} for {te}", tr.Name, te.Name);
        }
    }

    public Window? GetEditorWindow(Type recordType, object record)
    {
        _log.LogInformation("Looking for editor for record type {Type} ({RecordType})", recordType.Name, record.GetType().Name);
        if (!_editorTypes.TryGetValue(recordType, out var windowType))
        {
            _log.LogWarning("Did not find an editor");
            return null;
        }

        var ctor = windowType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, binder: null, types: [recordType], modifiers: null);
        if (ctor is null)
        {
            _log.LogWarning("Did not find a fitting ctor");
            return null;
        }

        var instance = ctor.Invoke([record]);

        return (Window?)instance;
    }
    
    public Window? GetEditorWindow<T>(T record)
    {
        return record is null ? null : GetEditorWindow(typeof(T), record);
    }
}