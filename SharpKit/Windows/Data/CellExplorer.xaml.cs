using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Mutagen.Bethesda.Skyrim;
using Ookii.Dialogs.Wpf;
using SharpKit.Windows.Data.FormEditors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using ProgressBarStyle = Ookii.Dialogs.Wpf.ProgressBarStyle;

namespace SharpKit.Windows.Data;

public partial class CellExplorer : Window
{
    private readonly CellEditor _cellEditor;
    public bool ResizeLock { get; set; } = true;

    private CellExplorerVM _vm = new();
    
    public CellExplorer(CellEditor cellEditor)
    {
        _cellEditor = cellEditor;
        InitializeComponent();

        var spaces = PluginFileManager.Worldspaces.Value?.Select(w =>
            new CellExplorerVM.WorldspaceContainer(w, w.Name?.String ?? "<unknown>")).ToList() ?? [];


        var interiorItem = new CellExplorerVM.WorldspaceContainer(null, " Interiors");
        
        _vm.Worldspaces = [.. spaces.Prepend(interiorItem)];

        _vm.SelectedWorldspace = interiorItem;

        DataContext = _vm;

        var interiorCells = PluginFileManager.Cells.Value?
            .SelectMany(s => s.SubBlocks)
            .SelectMany(s => s.Cells)
            .Where(c => c.Flags.HasFlag(Cell.Flag.IsInteriorCell))
            .DistinctBy(c => c.EditorID)
            .OrderBy(c => c.EditorID)
            .ToList() ?? [];
        
        _vm.InteriorCells = [.. interiorCells];
        _vm.SelectedCells = _vm.InteriorCells;
        
        _searchTimer.Tick += (_, _) =>
        {
            _searchText = string.Empty;
            _searchTimer.Stop();
        };
    }

    private string _searchText = string.Empty;

    private readonly DispatcherTimer _searchTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private void CellList_KeyUp(object sender, KeyEventArgs e)
    {
        var letter = e.Key.ToString().ToLowerOrdinal();

        if (letter.Length != 1 || letter[0] < 'a' || letter[0] > 'z')
            return;

        _searchText += letter;

        // Restart the timeout after each keystroke.
        _searchTimer.Stop();
        _searchTimer.Start();

        var firstFound = _vm.SelectedCells.FirstOrDefault(c =>
            c.EditorID?.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase) ?? false);

        if (firstFound is null)
            return;

        CellGrid.SelectedItem = firstFound;
        CellGrid.ScrollIntoView(firstFound);
    }

    private void CellGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var selectedItem = CellGrid.SelectedItem;
        if (selectedItem is not ICellGetter cell) return;

        var progress = new ProgressDialog
        {
            WindowTitle = "Loading cell",
            Text = "The cell is loading, please wait...",
            Description = "Parsing cell contents",
            ShowTimeRemaining = false,
            ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar
        };

        progress.DoWork += (_, _) =>
        {
            _cellEditor.LoadCell(cell);    
        };

        progress.RunWorkerCompleted += (_, args) =>
        {
            progress.Dispose();
            
            if (args.Error is not null)
            {
                MessageBox.Show(
                    this,
                    $"Failed to parse plugin:\n{args.Error.GetBaseException().Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }
        };
        
        progress.ShowDialog(this);
    }
}