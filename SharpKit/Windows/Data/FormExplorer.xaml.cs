using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using SharpKit.Reflection;
using SharpKit.Util;
using Activator = Mutagen.Bethesda.Skyrim.Activator;
using DataGrid = System.Windows.Controls.DataGrid;
using Key = Mutagen.Bethesda.Skyrim.Key;
using Message = Mutagen.Bethesda.Skyrim.Message;
using MessageBox = System.Windows.MessageBox;

namespace SharpKit.Windows.Data;

public partial class FormExplorer : Window
{
    private readonly PluginFileManager _pfm;
    private readonly EditorProvider _edp;
    private FormExplorerVM _vm = new();

    private TreeItem? _currentFormType = null;
    
    public bool ResizeLock { get; set; } = true;
    
    public FormExplorer(PluginFileManager pfm, EditorProvider edp, WindowPosConfigManager wpcm)
    {
        _pfm = pfm;
        _edp = edp;
        InitializeComponent();
        
        SizeChanged += (_, _) =>
        {
            if (ResizeLock) return;
            // wpcm.UpdateRecordWindowPos((int)Left, (int)Width, (int)Top, (int)Height);
        };
        
        LocationChanged += (_, _) =>
        {
            if (ResizeLock) return;
            // wpcm.UpdateRecordWindowPos((int)Left, (int)Width, (int)Top, (int)Height);
        };
        
        _vm.Items.Add(new TreeItem("Actors", null, [
            new TreeItem("Actor", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Npcs)),
            new TreeItem("Actor Action", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Actions)),
            new TreeItem("Body Part Data", typeof(Ammunition), null, mod => mod.SelectMany(m => m.BodyParts)),
            new TreeItem("Leveled Character", typeof(Ammunition), null, mod => mod.SelectMany(m => m.LeveledNpcs)),
            new TreeItem("Perk", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Perks)),
            new TreeItem("Talking Activator", typeof(Ammunition), null, mod => mod.SelectMany(m => m.TalkingActivators)),
        ]));
        
        _vm.Items.Add(new TreeItem("Audio", null, [
            new TreeItem("Acoustic Space", typeof(Ammunition), null, mod => mod.SelectMany(m => m.AcousticSpaces)),
            new TreeItem("Music Track", typeof(Ammunition), null, mod => mod.SelectMany(m => m.MusicTracks)),
            new TreeItem("Music Type", typeof(Ammunition), null, mod => mod.SelectMany(m => m.MusicTypes)),
            new TreeItem("Reverb Parameters", typeof(Ammunition), null, mod => mod.SelectMany(m => m.ReverbParameters)),
            new TreeItem("Sound Category", typeof(Ammunition), null, mod => mod.SelectMany(m => m.SoundCategories)),
            new TreeItem("Sound Descriptor", typeof(Ammunition), null, mod => mod.SelectMany(m => m.SoundDescriptors)),
            new TreeItem("Sound Marker", typeof(Ammunition), null, mod => mod.SelectMany(m => m.SoundMarkers)),
            new TreeItem("Sound Output Model", typeof(Ammunition), null, mod => mod.SelectMany(m => m.SoundOutputModels)),
        ]));
        
        _vm.Items.Add(new TreeItem("Character", null, [
            new TreeItem("Association Type", typeof(Ammunition), null, mod => mod.SelectMany(m => m.AssociationTypes)),
            new TreeItem("Class", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Classes)),
            new TreeItem("Equip Slot", typeof(Ammunition), null, mod => mod.SelectMany(m => m.EquipTypes)),
            new TreeItem("Faction", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Factions)),
            new TreeItem("Head Part", typeof(Ammunition), null, mod => mod.SelectMany(m => m.HeadParts)),
            new TreeItem("Movement Type", typeof(Ammunition), null, mod => mod.SelectMany(m => m.MovementTypes)),
            new TreeItem("Package", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Packages)),
            new TreeItem("Quest", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Quests)),
            new TreeItem("Race", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Races)),
            new TreeItem("Relationship", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Relationships)),
            new TreeItem("SM Event Node", typeof(Ammunition), null, mod => mod.SelectMany(m => m.StoryManagerEventNodes)),
            new TreeItem("Voice Type", typeof(Ammunition), null, mod => mod.SelectMany(m => m.VoiceTypes)),
        ]));
        
        _vm.Items.Add(new TreeItem("Items", null, [
            new TreeItem("Ammo", typeof(Ammunition), null, mod => mod.SelectMany(m => m.Ammunitions)),
            new TreeItem("Armor", typeof(Armor), null, mod => mod.SelectMany(m => m.Armors)),
            new TreeItem("Armor Addon", typeof(ArmorAddon), null, mod => mod.SelectMany(m => m.ArmorAddons)),
            new TreeItem("Book", typeof(Book), null, mod => mod.SelectMany(m => m.Books)),
            new TreeItem("Constructible Object", typeof(ConstructibleObject), null, mod => mod.SelectMany(m => m.ConstructibleObjects)),
            new TreeItem("Ingredient", typeof(Ingredient), null, mod => mod.SelectMany(m => m.Ingredients)),
            new TreeItem("Key", typeof(Key), null, mod => mod.SelectMany(m => m.Keys)),
            new TreeItem("Leveled Item", typeof(LeveledItem), null, mod => mod.SelectMany(m => m.LeveledItems)),
            new TreeItem("Misc Item", typeof(MiscItem), null, mod => mod.SelectMany(m => m.MiscItems)),
            new TreeItem("Outfit", typeof(Outfit), null, mod => mod.SelectMany(m => m.Outfits)),
            new TreeItem("Soul Gem", typeof(SoulGem), null, mod => mod.SelectMany(m => m.SoulGems)),
            new TreeItem("Weapon", typeof(Weapon), null, mod => mod.SelectMany(m => m.Weapons)),
        ]));
        
        _vm.Items.Add(new TreeItem("Magic", null, [
            new TreeItem("Dual Cast Data", typeof(DualCastData), null, mod => mod.SelectMany(m => m.DualCastData)),
            new TreeItem("Leveled Spell", typeof(LeveledSpell), null, mod => mod.SelectMany(m => m.LeveledSpells)),
            new TreeItem("Magic Effect", typeof(MagicEffect), null, mod => mod.SelectMany(m => m.MagicEffects)),
            new TreeItem("Scroll", typeof(Scroll), null, mod => mod.SelectMany(m => m.Scrolls)),
            new TreeItem("Shout", typeof(Shout), null, mod => mod.SelectMany(m => m.Shouts)),
            new TreeItem("Spell", typeof(Spell), null, mod => mod.SelectMany(m => m.Spells)),
            new TreeItem("Word of Power", typeof(WordOfPower), null, mod => mod.SelectMany(m => m.WordsOfPower)),
        ]));
        
        _vm.Items.Add(new TreeItem("Miscellaneous", null, [
            new TreeItem("Anim Object", typeof(AnimatedObject), null, mod => mod.SelectMany(m => m.AnimatedObjects)),
            new TreeItem("Art Object", typeof(ArtObject), null, mod => mod.SelectMany(m => m.ArtObjects)),
            new TreeItem("Collision Layer", typeof(CollisionLayer), null, mod => mod.SelectMany(m => m.CollisionLayers)),
            new TreeItem("Color Form", typeof(ColorRecord), null, mod => mod.SelectMany(m => m.Colors)),
            new TreeItem("Combat Style", typeof(CombatStyle), null, mod => mod.SelectMany(m => m.CombatStyles)),
            new TreeItem("Form List", typeof(FormList), null, mod => mod.SelectMany(m => m.FormLists)),
            new TreeItem("Global", typeof(Global), null, mod => mod.SelectMany(m => m.Globals)),
            new TreeItem("Idle marker", typeof(IdleMarker), null, mod => mod.SelectMany(m => m.IdleMarkers)),
            new TreeItem("Keyword", typeof(Keyword), null, mod => mod.SelectMany(m => m.Keywords)),
            new TreeItem("Land Texture", typeof(LandscapeTexture), null, mod => mod.SelectMany(m => m.LandscapeTextures)),
            new TreeItem("Load Screen", typeof(LoadScreen), null, mod => mod.SelectMany(m => m.LoadScreens)),
            new TreeItem("Material Object", typeof(MaterialObject), null, mod => mod.SelectMany(m => m.MaterialObjects)),
            new TreeItem("Message", typeof(Message), null, mod => mod.SelectMany(m => m.Messages)),
        ]));
        
        _vm.Items.Add(new TreeItem("Special Effect", null, [
            new TreeItem("AddOn Node", typeof(AddonNode), null, mod => mod.SelectMany(m => m.AddonNodes)),
            new TreeItem("Camera Shot", typeof(CameraShot), null, mod => mod.SelectMany(m => m.CameraShots)),
            new TreeItem("Debris", typeof(Debris), null, mod => mod.SelectMany(m => m.Debris)),
            new TreeItem("Effect Shader", typeof(EffectShader), null, mod => mod.SelectMany(m => m.EffectShaders)),
            new TreeItem("Explosion", typeof(Explosion), null, mod => mod.SelectMany(m => m.Explosions)),
            new TreeItem("Footstep", typeof(Footstep), null, mod => mod.SelectMany(m => m.Footsteps)),
            new TreeItem("Footstep Set", typeof(FootstepSet), null, mod => mod.SelectMany(m => m.FootstepSets)),
            new TreeItem("Hazard", typeof(Hazard), null, mod => mod.SelectMany(m => m.Hazards)),
            new TreeItem("Imagespace", typeof(ImageSpace), null, mod => mod.SelectMany(m => m.ImageSpaces)),
            new TreeItem("Impact Data Set", typeof(ImpactDataSet), null, mod => mod.SelectMany(m => m.ImpactDataSets)),
            new TreeItem("Lens Flare", typeof(LensFlare), null, mod => mod.SelectMany(m => m.LensFlares)),
            new TreeItem("Material Type", typeof(MaterialType), null, mod => mod.SelectMany(m => m.MaterialTypes)),
            new TreeItem("Projectile", typeof(Projectile), null, mod => mod.SelectMany(m => m.Projectiles)),
            new TreeItem("Volumetric Lighting", typeof(VolumetricLighting), null, mod => mod.SelectMany(m => m.VolumetricLightings)),
        ]));

        _vm.Items.Add(new TreeItem("World Data", null, [
            new TreeItem("Climate", typeof(Climate), null, mod => mod.SelectMany(m => m.Climates)),
            new TreeItem("Encounter Zone", typeof(EncounterZone), null, mod => mod.SelectMany(m => m.EncounterZones)),
            new TreeItem("Lighting Template", typeof(LightingTemplate), null, mod => mod.SelectMany(m => m.LightingTemplates)),
            new TreeItem("Location", typeof(Location), null, mod => mod.SelectMany(m => m.Locations)),
            new TreeItem("Location Ref Type", typeof(LocationReferenceType), null, mod => mod.SelectMany(m => m.LocationReferenceTypes)),
            new TreeItem("Shader Particle Geometry", typeof(ShaderParticleGeometry), null, mod => mod.SelectMany(m => m.ShaderParticleGeometries)),
            new TreeItem("Visual Effect", typeof(VisualEffect), null, mod => mod.SelectMany(m => m.VisualEffects)),
            new TreeItem("Weather", typeof(Weather), null, mod => mod.SelectMany(m => m.Weathers)),
        ]));

        _vm.Items.Add(new TreeItem("World Objects", null, [
            new TreeItem("Activator", typeof(Activator), null, mod => mod.SelectMany(m => m.Activators)),
            new TreeItem("Container", typeof(Container), null, mod => mod.SelectMany(m => m.Containers)),
            new TreeItem("Door", typeof(Door), null, mod => mod.SelectMany(m => m.Doors)),
            new TreeItem("Flora", typeof(Flora), null, mod => mod.SelectMany(m => m.Florae)),
            new TreeItem("Furniture", typeof(Furniture), null, mod => mod.SelectMany(m => m.Furniture)),
            new TreeItem("Grass", typeof(Grass), null, mod => mod.SelectMany(m => m.Grasses)),
            new TreeItem("Light", typeof(Light), null, mod => mod.SelectMany(m => m.Lights)),
            new TreeItem("Movable Static", typeof(MoveableStatic), null, mod => mod.SelectMany(m => m.MoveableStatics)),
            new TreeItem("Static", typeof(Static), null, mod => mod.SelectMany(m => m.Statics)),
            new TreeItem("Tree", typeof(Tree), null, mod => mod.SelectMany(m => m.Trees)),
        ]));
        
        DataContext = _vm;

        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FormExplorerVM.Search))
            {
                DoSearch();
            }
                
        };
    }

    private void DoSearch()
    {
        if (string.IsNullOrEmpty(_vm.Search)) return;
        _vm.FilteredRows = [.. _vm.Rows.Where(r => r.EditorID.ContainsInsensitive(_vm.Search))];
    }

    private void FormTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeItem item) return;
        var activeMod = _pfm.GetActiveMod();
        if (activeMod is null || item.RecordType is null)
        {

            return;
        }

        _currentFormType = item;

        var deps = _pfm.GetDependencies();
        if (item.GetRecords is null || deps is null) return;
        
        var it = item.GetRecords(deps);
            
        var items = it
            .Where(x => x.EditorID is not null)
            .Select(x => new TreeRowItem
                { EditorID = x.EditorID ?? "", FormID = x.FormKey.ID, Count = 0, Users = 0, Instance = x });
        
        _vm.Rows = [.. items];
        _vm.FilteredRows = _vm.Rows;
    }

    private void List_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        var element = e.OriginalSource as DependencyObject;
        while (element is not null && element is not DataGridRow)
            element = VisualTreeHelper.GetParent(element);

        if (element is not DataGridRow row)
            return;

        if (row.Item is not TreeRowItem item || _currentFormType is null)
            return;

        if (_currentFormType.RecordType is null || item.Instance is null)
            return;

        var editorWindow = _edp.GetEditorWindow(_currentFormType.RecordType, item.Instance);
        if (editorWindow is null)
        {
            MessageBox.Show("Editing this form type is not supported yet.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Asterisk);   
        }
        else
        {
            editorWindow.Owner = this;
            editorWindow.Show();
        }
    }
}