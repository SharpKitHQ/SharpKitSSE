using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using SharpKit.Reflection;
using SharpKit.Util;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace SharpKit.Windows.Data.FormEditors.Miscellaneous;

[RecordEditor(typeof(Keyword))]
public partial class KeywordEditor : DialogWindow, INotifyPropertyChanged
{
    public IKeywordGetter? Keyword { get; private set; }
    
    public string ID { get; set; } = "";

    public bool Result { get; set; } = false;
    
    public Color Color
    {
        get;
        set
        {
            field = value;
            ColorBrush = new SolidColorBrush(value);
            OnPropertyChanged(nameof(Color));
        }
    }

    private Brush _colorBrush = Brushes.Gray;

    public Brush ColorBrush
    {
        get => _colorBrush;
        private set
        {
            _colorBrush = value;
            OnPropertyChanged();
        }
    }
    
    private readonly PluginFileManager _pfm;
    
    
    public KeywordEditor(IKeywordGetter keyword)
    {
        Keyword = keyword;
        InitializeComponent();

        ID = keyword.EditorID ?? "";
        Color = keyword.Color is not null? Color.FromArgb(255, keyword.Color.Value.R, keyword.Color.Value.G, keyword.Color.Value.B) : Color.FromArgb(255, 100, 100, 100);
        
        DataContext = this;
        _pfm = App.ServiceProvider!.GetRequiredService<PluginFileManager>();
    }

    public KeywordEditor()
    {
        InitializeComponent();
        _pfm = App.ServiceProvider!.GetRequiredService<PluginFileManager>();
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Keyword is null)
        {
            Keyword = _pfm.GetActiveMod()?.Keywords.AddNew(ID);
            if (Keyword is Keyword k)
            {
                k.Color = System.Drawing.Color.FromArgb(Color.A, Color.R, Color.G, Color.B);
                DialogResult = true;
                Close();
                return;
            }
        }
        
        DialogResult = false;
        
        Close();
    }

    private void PickColorBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new ColorDialog();
        dialog.AllowFullOpen = true;
        dialog.FullOpen = true;
        dialog.AnyColor = true;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            Color = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void CancelBtn_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}