using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;
using Ammunition = Mutagen.Bethesda.Skyrim.Ammunition;

namespace SharpKit.Windows.Data.FormEditors.Items;

[RecordEditor(typeof(Ammunition))]
public partial class AmmoItemEditor : Window
{
    public string? EditorID { get; set; }
    
    public string? ItemName { get; set; }
    
    public string? ShortName { get; set; }
    
    public uint Value { get; set; }
    
    public float Damage { get; set; }
    
    public float Weight { get; set; }
    
    public IProjectileGetter? Projectile { get; set; }
    
    public bool IgnoresNormalWeaponRes { get; set; }
    
    public bool Playable { get; set; }
    
    public bool Bolt { get; set; }
    
    public ISoundGetter PickupSound { get; set; }
    
    public ISoundGetter PutdownSound { get; set; }
    
    public string? InventoryImage { get; set; }
    
    public string? MessageIcon { get; set; }
    
    public string? Model { get; set; }
    
    public string? Description { get; set; }
    
    public List<IKeywordGetter> Keywords { get; set; }
    
    public List<IProjectileGetter> Projectiles { get; set; }
    
    public AmmoItemEditor(IAmmunitionGetter ammo)
    {
        InitializeComponent();

        var mgr = App.ServiceProvider?.GetRequiredService<PluginFileManager>() ?? throw new Exception();
        
        var env = mgr.GetEnv() ?? throw new Exception();
        var mod = mgr.GetActiveMod() ?? throw new Exception();
        var deps = mgr.GetDependencies() ?? throw new Exception();

        EditorID = ammo.EditorID;
        ItemName = ammo.Name?.String;
        ShortName = ammo.ShortName;
        Value = ammo.Value;
        Damage = ammo.Damage;
        Weight = ammo.Weight;
        Projectile = ammo.Projectile.Resolve(env.LinkCache);
        IgnoresNormalWeaponRes = ammo.Flags.HasFlag(Ammunition.Flag.IgnoresNormalWeaponResistance);
        Playable = !ammo.Flags.HasFlag(Ammunition.Flag.NonPlayable);
        Bolt = !ammo.Flags.HasFlag(Ammunition.Flag.NonBolt);
        PickupSound = ammo.PickUpSound.Resolve(env.LinkCache);
        PutdownSound = ammo.PickUpSound.Resolve(env.LinkCache);
        InventoryImage = ammo.Icons?.LargeIconFilename;
        MessageIcon = ammo.Icons?.SmallIconFilename;
        Model = ammo.Model?.File.GivenPath;
        Description = ammo.Description?.String;
        Keywords = [.. ammo.Keywords?.Select(k => k.Resolve(env.LinkCache)) ?? []];
        Projectiles = deps.SelectMany(x => x.Projectiles).ToList();

        DataContext = this;
    }
}