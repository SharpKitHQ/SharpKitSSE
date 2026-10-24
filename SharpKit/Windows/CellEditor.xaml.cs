using System.Numerics;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Controls;
using SharpKit.Renderer;

namespace SharpKit.Windows;

public partial class CellEditor : Window
{
    private readonly PluginFileManager _pfm;
    private readonly Dx11Panel _panel;
    private readonly TriangleRenderer _renderer;
    private bool _initialized;
    
    public CellEditor(PluginFileManager pfm)
    {
        _pfm = pfm;
        InitializeComponent();

        var dirPath = _pfm.GetEnv().DataFolderPath;

        _renderer = new(App.ServiceProvider!.GetRequiredService<ILogger<TriangleRenderer>>(), dirPath);
        
        _panel = new(_renderer);
        _panel.Dock = DockStyle.Fill;
        RenderHost.Child = _panel;
        
        Loaded += OnLoaded;
        Closed += OnClosed;
        _panel.SizeChanged += OnPanelSizeChanged;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0)
            return;
        
        _renderer.Initialize(_panel.Handle, (uint)_panel.ClientSize.Width, (uint)_panel.ClientSize.Height);

        _initialized = true;
        //
        // var foundCell = (PluginFileManager.Cells.Value ?? [])
        //     .SelectMany(cellBlock => cellBlock.SubBlocks)
        //     .SelectMany(subBlock => subBlock.Cells)
        //     .FirstOrDefault(cell => cell.EditorID?.Equals("qasmoke", StringComparison.OrdinalIgnoreCase) == true);
        //
        // if (foundCell is null)
        //     throw new Exception("could not find qasmoke");
        
        
        // _renderer.LoadModel(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Meshes\architecture\farmhouse\farmhouse01.nif", new Vector3(0, 0, 0));
        // _renderer.LoadModel(@"G:\SteamLibrary\steamapps\common\Skyrim Special Edition\Data\Meshes\architecture\farmhouse\farmhouse02.nif", new Vector3(1000, 10, 10));
        
        // _renderer.LoadCell(foundCell, App.ServiceProvider!.GetRequiredService<PluginFileManager>().GetEnv());
    }

    public void LoadCell(ICellGetter cell)
    {
        _renderer.LoadCell(cell, _pfm.GetEnv());
    }

    private void OnPanelSizeChanged(object? sender, EventArgs e)
    {
        if (!_initialized || _panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0)
            return;

        _renderer.Resize((uint)_panel.ClientSize.Width, (uint)_panel.ClientSize.Height);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _panel.SizeChanged -= OnPanelSizeChanged;
        _renderer.Dispose();
    }
}