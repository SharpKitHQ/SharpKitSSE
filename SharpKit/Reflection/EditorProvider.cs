using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpKit.Util;

namespace SharpKit.Reflection;

public class EditorProvider
{
    private readonly ILogger<EditorProvider> _log;
    private readonly IServiceProvider _sp;
    private readonly Dictionary<Type, Type> _editorTypes;
    private readonly Dictionary<Type, Type> _recordManagers;

    public EditorProvider(ILogger<EditorProvider> log, IServiceProvider sp)
    {
        _log = log;
        _sp = sp;
        log.LogInformation("Looking for record editors...");
        
        _editorTypes = GetType().Assembly.GetTypes()
            .Select(t => new
            {
                Type = t,
                Interface = t.GetInterfaces()
                    .FirstOrDefault(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IRecordEditorWindow<>))
            })
            .Where(x => x.Interface is not null)
            .ToDictionary(
                x => x.Interface!.GetGenericArguments()[0],
                x => x.Type);

        _recordManagers = GetType().Assembly.GetTypes()
            .Select(t => new
            {
                Type = t,
                Attribute = t.GetCustomAttribute<RecordManagerAttribute>()
            })
            .Where(x => x.Attribute is not null)
            .Select(x => new KeyValuePair<Type, Type>(x.Attribute!.Type, x.Type))
            .ToDictionary();
        
        log.LogInformation("Found {Count} editors", _editorTypes.Count);
        log.LogInformation("Found {Count} record managers", _recordManagers.Count);
    }

    public IRecordManager? GetRecordManager(Type recordType)
    {
        if (_recordManagers.TryGetValue(recordType, out var managerType))
        {
            var instance = ActivatorUtilities.CreateInstance(_sp, managerType);
            if (instance is IRecordManager m)
                return m;
        }

        return null;
    }

    public IRecordEditorWindow<T>? GetEditorWindow<T>(T record) where T : class
    {
        _log.LogDebug("Looking for editor for record type {Type} ({RecordType})", typeof(T).Name, record.GetType().Name);

        if (!_editorTypes.TryGetValue(typeof(T), out var windowType))
        {
            _log.LogWarning("Did not find an editor for {Type}", typeof(T).Name);
            return null;
        }

        if (record is not { } typedRecord)
        {
            _log.LogWarning("Record of type {RecordType} is not compatible with {Type}", record.GetType().Name, typeof(T).Name);
            return null;
        }

        var ctor = windowType.GetConstructor([typeof(T)]);

        if (ctor is null)
        {
            _log.LogWarning("No constructor accepting {Type} found on {WindowType}", typeof(T).Name, windowType.Name);
            return null;
        }

        try
        {
            var instance = ctor.Invoke([typedRecord]);

            if (instance is IRecordEditorWindow<T> editor)
                return editor;

            _log.LogWarning("{WindowType} does not implement IRecordEditorWindow<{Type}>", windowType.Name, typeof(T).Name);

            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to create editor {WindowType} for {Type}", windowType.Name, typeof(T).Name);

            return null;
        }
    }
    
    public IRecordEditorWindow<T>? GetEditorWindow<T>() where T : class
    {
        _log.LogInformation("Looking for editor for record type {Type}", typeof(T).Name);

        if (!_editorTypes.TryGetValue(typeof(T), out var windowType))
        {
            _log.LogWarning("Did not find an editor for {Type}", typeof(T).Name);
            return null;
        }

        var ctor = windowType.GetConstructor(Type.EmptyTypes);

        if (ctor is null)
        {
            _log.LogWarning("No parameterless constructor found on {WindowType}", windowType.Name);
            return null;
        }

        try
        {
            var instance = ctor.Invoke(null);

            if (instance is IRecordEditorWindow<T> editor)
                return editor;

            _log.LogWarning("{WindowType} does not implement IRecordEditorWindow<{Type}>", windowType.Name, typeof(T).Name);

            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to create editor {WindowType} for {Type}", windowType.Name, typeof(T).Name);

            return null;
        }
    }
    
    public IRecordEditorWindow? GetEditorWindow(Type recordType)
    {
        _log.LogInformation("Looking for editor for record type {Type}", recordType.Name);

        if (!_editorTypes.TryGetValue(recordType, out var windowType))
        {
            _log.LogWarning("Did not find an editor for {Type}", recordType.Name);
            return null;
        }

        var ctor = windowType.GetConstructor(Type.EmptyTypes);

        if (ctor is null)
        {
            _log.LogWarning("No parameterless constructor found on {WindowType}", windowType.Name);
            return null;
        }

        try
        {
            if (ctor.Invoke(null) is IRecordEditorWindow editor)
                return editor;

            _log.LogWarning("{WindowType} does not implement IRecordEditorWindow", windowType.Name);

            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to create editor {WindowType} for {Type}", windowType.Name, recordType.Name);

            return null;
        }
    }
    
    public IRecordEditorWindow? GetEditorWindow(object record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var recordType = record.GetType();

        _log.LogInformation("Looking for editor for record type {Type}", recordType.Name);

        if (!_editorTypes.TryGetValue(recordType, out var windowType))
        {
            _log.LogWarning("Did not find an editor for {Type}", recordType.Name);
            return null;
        }

        var ctor = windowType
            .GetConstructors()
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();

                return parameters.Length == 1 &&
                       parameters[0].ParameterType.IsInstanceOfType(record);
            });

        if (ctor is null)
        {
            _log.LogWarning("No compatible single-argument constructor found on {WindowType} for {RecordType}", windowType.Name, recordType.Name);
            return null;
        }

        try
        {
            if (ctor.Invoke([record]) is IRecordEditorWindow editor)
                return editor;

            _log.LogWarning("{WindowType} does not implement IRecordEditorWindow", windowType.Name);

            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to create editor {WindowType} for {RecordType}", windowType.Name, recordType.Name);

            return null;
        }
    }
}