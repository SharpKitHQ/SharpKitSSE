using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Exceptions;

namespace SharpKit;

public class PluginFileManager : IDisposable
{
    // ReSharper disable once NotAccessedField.Local
    private readonly ILogger<PluginFileManager> _log;
    private IGameEnvironment<ISkyrimMod, ISkyrimModGetter> _env;

    private SkyrimMod? _mod;
    private string? _modFileName;

    private string _dataPath;
    
    private readonly SemaphoreSlim _lock = new(1);

    private List<ISkyrimModGetter>? _dependencies;

    public static Lazy<List<IObjectEffectGetter>?> Enchantments { get; private set; } = new(() => null);
    
    public static Lazy<List<IArmorGetter>?> Armors { get; private set; } = new(() => null);
    
    public static Lazy<List<IEquipTypeGetter>?> EquipTypes { get; private set; } = new(() => null);
    
    public static Lazy<List<IImpactDataSetGetter>?> BashDatas { get; private set; } = new(() => null);
    
    public static Lazy<List<IMaterialTypeGetter>?> BlockMaterials { get; private set; } = new(() => null);
    
    public static Lazy<List<IRaceGetter>?> Races { get; set; } = new(() => null);

    public static Lazy<List<ICellBlockGetter>?> Cells { get; set; } = new(() => null);

    public static Lazy<List<IWorldspaceGetter>?> Worldspaces { get; set; } = new(() => null);

    public static Lazy<List<IKeywordGetter>?> Keywords { get; set; } = new(() => null);

    public static Lazy<List<ITextureSetGetter>?> TextureSets { get; set; } = new(() => null);
    
    public PluginFileManager(ILogger<PluginFileManager> log)
    {
        Enchantments = new(() => _dependencies?.SelectMany(d => d.ObjectEffects).ToList() ?? []);
        Armors = new(() => _dependencies?.SelectMany(d => d.Armors).ToList() ?? []);
        EquipTypes = new(() => _dependencies?.SelectMany(d => d.EquipTypes).ToList() ?? []);
        BashDatas = new(() => _dependencies?.SelectMany(d => d.ImpactDataSets).ToList() ?? []);
        BlockMaterials = new(() => _dependencies?.SelectMany(d => d.MaterialTypes).ToList() ?? []);
        Races = new(() => _dependencies?.SelectMany(d => d.Races).ToList() ?? []);
        Cells = new(() => _dependencies?.SelectMany(d => d.Cells).ToList() ?? []);
        Worldspaces = new(() => _dependencies?.SelectMany(d => d.Worldspaces).ToList() ?? []);
        Keywords = new(() => _dependencies?.SelectMany(d => d.Keywords).ToList() ?? []);
        TextureSets = new(() => _dependencies?.SelectMany(d => d.TextureSets).ToList() ?? []);
        
        _log = log;
        _env = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
        _dataPath = _env.DataFolderPath;
    }

    public IEnumerable<IMajorRecordGetter>? GetRecordsByType(Type type)
    {
        return type switch
        {
            _ when type == typeof(Keyword) => _env.LoadOrder.PriorityOrder.Keyword().WinningOverrides(),
            _ => null
        };
    }

    public IGameEnvironment GetEnv()
    {
        _lock.Wait();
        try
        {
            return _env;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public SkyrimMod? GetActiveMod()
    {
        _lock.Wait();
        try
        {
            return _mod;
        }
        finally
        {
            _lock.Release();
        }
    }

    public string? GetActiveFileName()
    {
        _lock.Wait();
        try
        {
            return _modFileName;
        }
        finally
        {
            _lock.Release();
        }
    }

    public List<ISkyrimModGetter>? GetDependencies()
    {
        _lock.Wait();
        try
        {
            return _dependencies;
        }
        finally
        {
            _lock.Release();
        }
    }

    public SkyrimMod CreateMod(string name, List<string>? attachMasters = null, IProgress<string>? progress = null)
    {
        _lock.Wait();
        try
        {
            _mod = new SkyrimMod(
                ModKey.FromFileName(name),
                SkyrimRelease.SkyrimSE);
            _modFileName = name;

            ReplaceEnvironment(_mod);

            if (attachMasters is not null && attachMasters.Count > 0)
            {
                foreach (var m in attachMasters)
                {
                    AttachMaster(m);
                }
            }

            _dependencies = GetActiveModDependencies();
            
            return _mod;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void AttachMaster(string fileName)
    {
        if (_mod is null) throw new NoActiveFileException();

        var masterKey = ModKey.FromFileName(fileName);

        if (masterKey == _mod.ModKey)
        {
            _log.LogWarning("Not setting {FileName} as its own master", fileName);
            return;
        }

        if (!_env.LoadOrder.ContainsKey(masterKey))
            throw new FileNotFoundException($"Master '{fileName}' not found");
        
        var listing = _env.LoadOrder
            .FirstOrDefault(x => x.Key == masterKey);

        if (listing is null || !listing.Value.Enabled || !listing.Value.ModExists)
        {
            throw new InvalidOperationException(
                $"Master '{fileName}' must be enabled and exist on disk.");
        }

        var masters = _mod.ModHeader.MasterReferences
            .Select(x => x.Master)
            .Append(masterKey)
            .Distinct()
            .OrderBy(x => _env.LoadOrder.IndexOf(x))
            .ToList();

        _mod.ModHeader.MasterReferences.Clear();

        foreach (var master in masters)
        {
            _mod.ModHeader.MasterReferences.Add(
                new MasterReference { Master = master });
        }
    }
    
    public SkyrimMod LoadMod(string fileName, List<string>? attachMasters = null, IProgress<string>? progress = null)
    {
        _lock.Wait();
        try
        {
            progress?.Report($"Loading plugin file {fileName}...");
            
            var modKey = ModKey.FromFileName(fileName);
            var path = Path.Combine(_dataPath, modKey.FileName);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Plugin '{fileName}' was not found.", path);
            
            _log.LogInformation("Loading plugin {Name} from {Path}", fileName, path);

            var mod = SkyrimMod.CreateFromBinary(
                path,
                SkyrimRelease.SkyrimSE);

            _mod = mod;
            _modFileName = fileName;
            
            progress?.Report("Parsing plugin file...");
            
            _log.LogInformation("Updating env...");
            ReplaceEnvironment(mod);

            _log.LogInformation("Attaching masters...");
            if (attachMasters is not null && attachMasters.Count > 0)
            {
                foreach (var m in attachMasters)
                {
                    _log.LogInformation("Attaching {Name} as master", m);
                    AttachMaster(m);
                }
            }

            _log.LogInformation("Loaded plugin {Plugin} from {Path}, updating dependencies...",
                modKey.FileName, path);

            progress?.Report("Parsing plugin dependencies...");
            _dependencies = GetActiveModDependencies();
            
            return _mod;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void ReplaceEnvironment(SkyrimMod mod)
    {
        var oldEnv = _env;

        _env = GameEnvironment.Typical.Builder<ISkyrimMod, ISkyrimModGetter>(
                GameRelease.SkyrimSE)
            .WithOutputMod(mod)
            .Build();

        _dataPath = _env.DataFolderPath;
        oldEnv.Dispose();
    }

    public async Task SaveMod()
    {
        await _lock.WaitAsync();
        try
        {
            if (_mod is null)
                throw new NoActiveFileException();

            var outputPath = Path.Combine(_dataPath, _mod.ModKey.FileName);

            _log.LogInformation("Writing plugin {Plugin} to {Path}", _mod.ModKey.FileName, outputPath);

            await _mod.BeginWrite
                .ToPath(outputPath)
                .WithLoadOrder(_env.LoadOrder)
                .WriteAsync();    
        }
        finally
        {
            _lock.Release();
        }
    }

    public List<(string Key, ISkyrimModGetter? Mod, int Order)> GetPluginList()
    {
        return
            [.. _env.LoadOrder.Select(e => (e.Key.FileName, e.Value.Mod, _env.LoadOrder.IndexOf(e.Key))) ];
    }
    
    public List<ISkyrimModGetter> GetActiveModDependencies()
    {
        if (_mod is null)
            throw new NoActiveFileException();

        var result = new List<ISkyrimModGetter>();
        var visited = new HashSet<ModKey>();

        void AddDependencies(ISkyrimModGetter mod)
        {
            foreach (var master in mod.ModHeader.MasterReferences)
            {
                if (!visited.Add(master.Master))
                    continue;
                    
                var listing = _env.LoadOrder
                    .FirstOrDefault(x => x.Key == master.Master);

                if (listing?.Value.Mod is not { } masterMod)
                    throw new FileNotFoundException(
                        $"Master '{master.Master}' could not be loaded.");
                    
                AddDependencies(masterMod);
                result.Add(masterMod);
            }
        }

        AddDependencies(_mod);

        if (visited.Add(_mod.ModKey))
            result.Add(_mod);

        return result;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _env.Dispose();
    }
}