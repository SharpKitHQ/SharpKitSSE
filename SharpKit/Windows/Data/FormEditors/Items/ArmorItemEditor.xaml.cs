using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;
using Armor = Mutagen.Bethesda.Skyrim.Armor;
using IArmorGetter = Mutagen.Bethesda.Skyrim.IArmorGetter;

namespace SharpKit.Windows.Data.FormEditors.Items;

[RecordEditor(typeof(Armor))]
public partial class ArmorItemEditor : Window
{
    public Armor LocalCopy { get; set; }
    
    public IObjectEffectGetter? ObjectEffect { get; set; }
    
    public IArmorGetter? TemplateArmor { get; set; }
    
    public IEquipTypeGetter? EquipType { get; set; }
    
    public IImpactDataSetGetter? BashImpactData { get; set; }
    
    public IMaterialTypeGetter? BlockMaterial { get; set; }
    
    public IRaceGetter? Race { get; set; }

    public Lazy<List<IObjectEffectGetter>?> Enchantments { get; private init; } = PluginFileManager.Enchantments;
    public Lazy<List<IArmorGetter>?> Armors { get; private init; } = PluginFileManager.Armors;
    public Lazy<List<IEquipTypeGetter>?> EquipTypes { get; private init; } = PluginFileManager.EquipTypes;
    public Lazy<List<IImpactDataSetGetter>?> BashDatas { get; private init; } = PluginFileManager.BashDatas;
    public Lazy<List<IMaterialTypeGetter>?> BlockMaterials { get; private init; } = PluginFileManager.BlockMaterials;
    public Lazy<List<IRaceGetter>?> Races { get; private init; } = PluginFileManager.Races;

    public ArmorType[] ArmorTypes { get; private init; } = Enum.GetValues<ArmorType>();
    
    public uint BipedFlags { get; set; }
    
    public bool IsPlayable { get; set; }

    public ArmorItemEditor(IArmorGetter armor)
    {
        InitializeComponent();
        LocalCopy = armor.DeepCopy();

        var pfm = App.ServiceProvider?.GetRequiredService<PluginFileManager>() ?? throw new Exception();

        if (armor.ObjectEffect.TryResolve(pfm.GetEnv().LinkCache, out var effect))
            ObjectEffect = effect;

        if (armor.TemplateArmor.TryResolve(pfm.GetEnv().LinkCache, out var tarm))
            TemplateArmor = tarm;
        
        if(armor.EquipmentType.TryResolve(pfm.GetEnv().LinkCache, out var eq))
            EquipType = eq;

        if (armor.BashImpactDataSet.TryResolve(pfm.GetEnv().LinkCache, out var bb))
            BashImpactData = bb;

        if (armor.AlternateBlockMaterial.TryResolve(pfm.GetEnv().LinkCache, out var abm))
            BlockMaterial = abm;

        if (armor.Race.TryResolve(pfm.GetEnv().LinkCache, out var r))
            Race = r;
        
        // armor.BodyTemplate.ArmorType

        IsPlayable = !armor.MajorFlags.HasFlag(Armor.MajorFlag.NonPlayable);
        if(armor.BodyTemplate?.FirstPersonFlags is not null)
            BipedFlags = (uint)armor.BodyTemplate?.FirstPersonFlags!;

        DataContext = this;
    }
}