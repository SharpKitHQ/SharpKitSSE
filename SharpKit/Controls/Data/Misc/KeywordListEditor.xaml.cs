using System.Collections.ObjectModel;
using System.Windows;
using Mutagen.Bethesda.Skyrim;
using Serilog;
using SharpKit.Windows.Data.FormEditors.Miscellaneous;
using SharpKit.Windows.Data.FormEditors.Other;
using UserControl = System.Windows.Controls.UserControl;

namespace SharpKit.Controls.Data.Misc;

public partial class KeywordListEditor : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<IKeywordGetter>),
            typeof(KeywordListEditor),
            new PropertyMetadata(null));

    public ObservableCollection<IKeywordGetter>? ItemsSource
    {
        get => (ObservableCollection<IKeywordGetter>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    
    public KeywordListEditor()
    {
        InitializeComponent();
    }

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        var keywords = PluginFileManager.Keywords.Value ?? [];
        var dialog = new FormSelector(keywords)
        {
            ShowInTaskbar = false
        };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.SelectedItem is IKeywordGetter kywd)
            {
                ItemsSource?.Add(kywd);
            }
        }
    }

    private void New_OnClick(object sender, RoutedEventArgs e)
    {
        var editor = new KeywordEditor() { ShowInTaskbar = false };
        if (editor.ShowDialog() == true && editor.Keyword is not null)
        {
            ItemsSource?.Add(editor.Keyword);
        }
    }
}