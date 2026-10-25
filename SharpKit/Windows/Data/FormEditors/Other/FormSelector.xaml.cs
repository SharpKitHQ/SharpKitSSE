using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Mutagen.Bethesda.Skyrim;
using Serilog;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SharpKit.Windows.Data.FormEditors.Other;

public partial class FormSelector : Window, INotifyPropertyChanged
{
    public IReadOnlyList<ISkyrimMajorRecordGetter> AvailableItems { get; }

    public IReadOnlyList<ISkyrimMajorRecordGetter> FilteredItems
    {
        get;
        private set
        {
            if (Equals(field, value))
                return;

            field = value;
            OnPropertyChanged();
        }
    }

    public ISkyrimMajorRecordGetter? SelectedItem
    {
        get;
        set
        {
            if (Equals(field, value))
                return;

            field = value;
            OnPropertyChanged();
        }
    }

    public string SearchQuery
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged();
        }
    } = "";

    public FormSelector(IReadOnlyList<ISkyrimMajorRecordGetter> items)
    {
        InitializeComponent();
        
        AvailableItems = items;
        FilteredItems = items;
    }

    private void Search_KeyUp(object sender, KeyEventArgs e)
    {
        if (!string.IsNullOrEmpty(SearchQuery))
        {
            FilteredItems =
            [
                .. AvailableItems
                    .Where(x => x.EditorID?.StartsWith(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
            ];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = SelectedItem is not null;

        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}