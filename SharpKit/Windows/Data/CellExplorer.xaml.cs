using System.Windows;
using SharpKit.Util;

namespace SharpKit.Windows.Data;

public partial class CellExplorer : Window
{
    public bool ResizeLock { get; set; } = true;
    
    public CellExplorer(WindowPosConfigManager wpcm)
    {
        InitializeComponent();

        SizeChanged += (_, _) =>
        {
            if (ResizeLock) return;
            // wpcm.UpdateCellWindowPos((int)Left, (int)Width, (int)Top, (int)Height);
        };
        
        LocationChanged += (_, _) =>
        {
            if (ResizeLock) return;
            // wpcm.UpdateCellWindowPos((int)Left, (int)Width, (int)Top, (int)Height);
        };
    }
}