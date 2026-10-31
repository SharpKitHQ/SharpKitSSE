using System.Windows;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Util;
using SharpKit.Windows.Data.FormEditors.Other;

namespace SharpKit.Windows.Data.FormEditors.Items;

public partial class ArmorAddonEditor : RecordEditorWindow<IArmorAddonGetter>
{
    public IModelGetter? FemaleModel { get; set; }
    public IModelGetter? MaleModel { get; set; }
    
    public ArmorAddonEditor(IArmorAddonGetter armo)
    {
        InitializeComponent();

        FemaleModel = armo.WorldModel?.Female;
        MaleModel = armo.WorldModel?.Male;
    }

    private void Select_Female_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new ModelPicker(FemaleModel);
        picker.ShowDialog();
    }
}