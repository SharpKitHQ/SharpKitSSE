using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Ookii.Dialogs.Wpf;
using Serilog;
using SharpKit.Windows.Dialogs;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using ProgressBarStyle = Ookii.Dialogs.Wpf.ProgressBarStyle;

namespace SharpKit.Windows.Data;

public partial class DataFileSelectorWindow : Window
{
    private readonly PluginFileManager _pfm;
    private DataFileSelectorWindowVM _vm = new();
    
    public DataFileSelectorWindow(PluginFileManager pfm)
    {
        _pfm = pfm;
        InitializeComponent();

        var plugins = pfm.GetPluginList()
            .Select(p => new DataFileSelectorWindowVM.ModItem
            {
                Filename = p.Key,
                CreatedBy = p.Mod?.ModHeader.Author ?? "",
                Summary = p.Mod?.ModHeader.Description ?? "",
                ParentMasters = new ObservableCollection<string>(p.Mod?.ModHeader.MasterReferences
                    .Select(m => m.Master.FileName.String).ToList() ?? []),
                CreatedOn = DateTime.MinValue,
                LastModified = DateTime.MinValue,
                IsMaster = p.Mod?.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Master) ?? false,
                LoadOrder = p.Order
            })
            .OrderBy(p => p.LoadOrder)
            .ToList();

        _vm.Plugins = new ObservableCollection<DataFileSelectorWindowVM.ModItem>(plugins);
        _vm.FilteredPlugins = new ObservableCollection<DataFileSelectorWindowVM.ModItem>(plugins);
        
        DataContext = _vm;

        _vm.PropertyChanged += (s, p) =>
        {
            if (p.PropertyName == nameof(DataFileSelectorWindowVM.SearchString))
                DoSearch();
        };
    }

    private void DoSearch()
    {
        var query = _vm.SearchString.Trim();
        IEnumerable<DataFileSelectorWindowVM.ModItem> results = _vm.Plugins;

        if (!string.IsNullOrWhiteSpace(query))
        {
            results =
            [
                .. _vm.Plugins
                    .Where(p => p.Filename.ContainsInsensitive(query))
                    .OrderByDescending(p => p.LoadOrder)
            ];
        }

        _vm.FilteredPlugins = new ObservableCollection<DataFileSelectorWindowVM.ModItem>(results);
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count < 1) return;
        var realSelected = e.AddedItems[0];
        if (realSelected is not DataFileSelectorWindowVM.ModItem item) return;
        _vm.SelectedItem = item;
    }

    private void Selector_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        if (listBox.SelectedItem is not DataFileSelectorWindowVM.ModItem plugin)
            return;

        plugin.Selected = !plugin.Selected;
    }

    private void SetActiveFile_OnClick(object sender, RoutedEventArgs e)
    {

        if (Selector.SelectedItem is not DataFileSelectorWindowVM.ModItem plugin)
            return;

        _vm.ActiveItem = plugin;
    }
    
    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        var activeItem = _vm.ActiveItem;
        string? newFilename = null;

        if (activeItem is null)
        {
            if (MessageBox.Show(
                    "You haven't selected an active file, do you want to continue?",
                    "Are you sure?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            var dia = new TextInputDialog(
                "File Name",
                "Please enter the name for your new mod");

            if (dia.ShowDialog() != true)
                return;

            newFilename = dia.GetInput();

            if (!newFilename.EndsWith(".esp", StringComparison.OrdinalIgnoreCase))
                newFilename += ".esp";
        }

        var masters = _vm.FilteredPlugins
            .Where(p => p is { Selected: true, IsMaster: true })
            .Select(p => p.Filename)
            .ToList();

        var progress = new ProgressDialog
        {
            WindowTitle = "Loading Plugin",
            Text = "Please wait...",
            Description = "Parsing plugin data",
            ShowTimeRemaining = false,
            ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar
        };
        
        progress.DoWork += (_, args) =>
        {
            if (activeItem is not null)
            {
                _pfm.LoadMod(activeItem.Filename, masters, progress);
            }
            else
            {
                _pfm.CreateMod(newFilename!, masters, progress);
            }
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

            Log.Information("Found {N} cell blocks", PluginFileManager.Cells.Value?.Count);
            var cellBlocks = PluginFileManager.Cells.Value;
            
            
            DialogResult = true;
            Close();
        };

        progress.ShowDialog(this);
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}