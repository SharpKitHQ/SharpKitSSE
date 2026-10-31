using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Renderer;

/// <summary>Renders one Skyrim NIF against neutral studio lighting and a matte ground plane.</summary>
public sealed class PreviewRenderer : IDisposable
{
    private readonly MainRenderer _renderer;
    private readonly string _dataFolderPath;

    public PreviewRenderer(ILogger<MainRenderer> logger, string dataFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolderPath);
        _dataFolderPath = dataFolderPath;
        _renderer = new MainRenderer(logger, dataFolderPath, true);
    }

    public void Initialize(nint hwnd, uint width, uint height) => _renderer.Initialize(hwnd, width, height);

    /// <summary>Loads or replaces the model shown by this preview.</summary>
    public void LoadNif(string path, IList<ITextureSetGetter?>? textureSetOverrides = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _renderer.LoadPreviewModel(path, _dataFolderPath, textureSetOverrides);
    }

    public void ClearModel() => _renderer.ClearPreviewModel();

    public bool ShowGrid
    {
        get => _renderer.IsPreviewGridVisible;
        set => _renderer.SetPreviewGridVisible(value);
    }

    public void Render() => _renderer.Render();

    public void Resize(uint width, uint height) => _renderer.Resize(width, height);

    public void Orbit(float deltaX, float deltaY) => _renderer.Orbit(deltaX, deltaY);

    public void Pan(float deltaX, float deltaY) => _renderer.Pan(deltaX, deltaY);

    public void Zoom(float delta) => _renderer.Zoom(delta);

    public void Dispose() => _renderer.Dispose();
}
