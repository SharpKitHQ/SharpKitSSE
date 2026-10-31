using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Assets;
using NiflySharp;
using Serilog;
using SharpKit.Util;

namespace SharpKit.Windows.Data.FormEditors.Other;

/// <summary>
/// 3D Model Picker Dialog
/// </summary>
public partial class ModelPicker : Window
{
    public partial class TextureOverride : ObservableObject
    {
        [ObservableProperty] public partial string Name3D { get; set; } = string.Empty;
        
        [ObservableProperty] public partial int Index3D { get; set; }
        
        [ObservableProperty] public partial ITextureSetGetter? Texture { get; set; }
    }
    
    public static readonly DependencyProperty ModelPathProperty = DependencyProperty.Register(
        nameof(ModelPath), typeof(string), typeof(ModelPicker), new PropertyMetadata(null));

    public string? ModelPath
    {
        get => (string?)GetValue(ModelPathProperty);
        set => SetValue(ModelPathProperty, value);
    }

    public ObservableCollection<TextureOverride> AlternateTextures { get; set; } = [];

    private readonly CachedBSAFileProvider _fileProvider;

    private readonly PluginFileManager _pfm;

    public ObservableCollection<ITextureSetGetter?> TextureSetOverrides { get; set; } = [];
    
    /// <summary>
    /// Populated with an instance of IModelGetter constructed from selected paths and texture overrides after the user
    /// clicks the OK button.
    /// Null before or if Cancel is pressed.
    /// Check DialogResult
    /// </summary>
    public IModelGetter? Result { get; set; }
    
    public ModelPicker(IModelGetter? model)
    {
        InitializeComponent();

        _fileProvider = App.ServiceProvider?.GetRequiredService<CachedBSAFileProvider>()
            ?? throw new Exception("Service Provider not ready yet");

        _pfm = App.ServiceProvider.GetRequiredService<PluginFileManager>();

        ModelPath = model?.File.GivenPath;
        
        AlternateTextures.Clear();
        ProcessModel(model);
        
        foreach (var t in model?.AlternateTextures ?? [])
        {
            if (t.NewTexture.TryResolve(_pfm.GetEnv().LinkCache, out var nt))
            {
                var overridden = AlternateTextures.FirstOrDefault(at => at.Index3D == t.Index);
                overridden?.Texture = nt;
            }
        }

        UpdateAltList();
    }

    private void UpdateAltList()
    {
        foreach (var alternate in AlternateTextures)
        {
            if (alternate.Texture is not null && alternate.Index3D < TextureSetOverrides.Count)
                TextureSetOverrides[alternate.Index3D] = alternate.Texture;
        }
    }

    private void ProcessModel(IModelGetter? model)
    {
        if (model is null || model.File.IsNull)
        {
            Log.Warning("Model or Model File is null");
            return;
        }

        var nifFile = _fileProvider.GetFileFromAny(model.File.GivenPath);
        if (nifFile is null)
        {
            Log.Warning("Failed to load NIF from {Path}", model.File.GivenPath);
            return;
        }

        Log.Debug("Processing model {Path}", model.File.GivenPath);
        var nif = new NifFile(nifFile);

        var shapes = nif.GetShapes().ToList();
        for (var shapeIndex = 0; shapeIndex < shapes.Count; shapeIndex++)
            TextureSetOverrides.Add(null);

        for (var shapeIndex = 0; shapeIndex < shapes.Count; shapeIndex++)
        {
            var shape = shapes[shapeIndex];
            Log.Debug("Found shape {Name}", shape.Name.String);
            var shader = nif.GetShader(shape);
            if (shader is null || !shader.HasTextureSet) continue;
            Log.Debug("  Shape has a shader at {Idx}", shapeIndex);
            AlternateTextures.Add(new TextureOverride
            {
                Name3D = shape.Name.String,
                Index3D = shapeIndex,
                Texture = null
            });
        }
    }

    private void Grid_New_OnClick(object sender, RoutedEventArgs e)
    {
        if (TextureGrid.SelectedItem is not TextureOverride txo)
            return;

        var selector = new FormSelector(PluginFileManager.TextureSets.Value ?? []);
        if (selector.ShowDialog() == true)
        {
            var newOverride = selector.SelectedItem;
            if (newOverride is ITextureSetGetter tx)
            {
                txo.Texture = tx;
                UpdateAltList();
            }
        }
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ModelPath))
            return;
        
        var res = new Model
        {
            File = new AssetLink<SkyrimModelAssetType>(ModelPath)
        };

        res.AlternateTextures ??= [];
        
        foreach (var alt in AlternateTextures)
        {
            if (alt.Texture is not null)
            {
                res.AlternateTextures.Add(new AlternateTexture
                {
                    Index = alt.Index3D,
                    Name = alt.Name3D,
                    NewTexture = new FormLink<ITextureSetGetter>(alt.Texture)
                });
            }
        }

        Result = res;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Result = null;
    }
}
