using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using SharpKit.Reflection;
using SharpKit.Util;
using DataGrid = System.Windows.Controls.DataGrid;
using Key = Mutagen.Bethesda.Skyrim.Key;
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
        
        _vm.Items.Add(new TreeItem("Miscellaneous", null, [
            new TreeItem("Keyword", typeof(Keyword), null, mod => mod.SelectMany(m => m.Keywords)),
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