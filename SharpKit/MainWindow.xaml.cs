using System.Numerics;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DevZest.Windows.Docking;
using Microsoft.Extensions.DependencyInjection;
using SharpKit.Reflection;
using SharpKit.Util;
using SharpKit.Windows;
using SharpKit.Windows.Data;

namespace SharpKit;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IServiceProvider _sp;
    private readonly MainWindowVM _vm;
    private readonly PluginFileManager _pfm;
    private readonly EditorProvider _edp;
    private readonly WindowPosConfigManager _wpcm;

    public MainWindow(IServiceProvider sp, MainWindowVM vm, PluginFileManager pfm, EditorProvider edp, WindowPosConfigManager wpcm)
    {
        _sp = sp;
        _vm = vm;
        _pfm = pfm;
        _edp = edp;
        _wpcm = wpcm;
        InitializeComponent();

        DataContext = _vm;

        _vm.OpenDataWindow = new RelayCommand(() =>
        {
            var dialog = _sp.GetRequiredService<DataFileSelectorWindow>();
            if (dialog.ShowDialog() == true)
            {
                var activeFile = _pfm.GetActiveFileName();
                if (activeFile is null) return;
                _vm.Title = $"SharpKit [{activeFile}]";
                OnPluginLoad();
            }
        });
    }

    private void File_Data_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = _sp.GetRequiredService<DataFileSelectorWindow>();
        if (dialog.ShowDialog() == true)
        {
            var activeFile = _pfm.GetActiveFileName();
            if (activeFile is null) return;
            _vm.Title = $"SharpKit [{activeFile}]";
            OnPluginLoad();
        }
    }

    private void OnPluginLoad()
    {
        var formExplorer = new FormExplorer(_pfm, _edp, _wpcm)
        {
            Owner = this,
            ShowInTaskbar = false
        };
        formExplorer.Show();

        var cellExplorer = new CellExplorer(_wpcm)
        {
            Owner = this,
            ShowInTaskbar = false
        };
        cellExplorer.Show();
        
        var cellEditor = new CellEditor()
        {
            Owner = this,
            ShowInTaskbar = false
        };
        cellEditor.Show();
        
        formExplorer.Left = ((Width / 3) * 2) - 10;
        formExplorer.Width = (Width / 3) - 10;
        formExplorer.Top = 90;
        formExplorer.Height = (Height / 2) - 50;
        cellExplorer.Left = ((Width / 3) * 2) - 10;
        cellExplorer.Width = (Width / 3) - 10;
        cellExplorer.Top = 50 + (Height / 2);
        cellExplorer.Height = (Height / 2) - 90;
        
        cellEditor.Left = 10;
        cellEditor.Width = ((Width / 3) * 2) - 20;
        cellEditor.Top = 90;
        cellEditor.Height = Height - 120;

        formExplorer.ResizeLock = false;
        cellExplorer.ResizeLock = false;
    }
}