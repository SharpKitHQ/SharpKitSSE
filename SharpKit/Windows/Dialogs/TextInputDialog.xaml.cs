using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MessageBox = System.Windows.MessageBox;

namespace SharpKit.Windows.Dialogs;

public partial class TextInputDialog : Window
{
    private readonly bool _required;
    private readonly TextInputDialogVM _vm;

    public string GetInput() => _vm.Input;
    
    public TextInputDialog(string title, string description, bool required = true)
    {
        _required = required;
        InitializeComponent();
        _vm = new() { Title = title, Description = description };
        DataContext = _vm;
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        if (_required && string.IsNullOrWhiteSpace(_vm.Input))
        {
            MessageBox.Show("Please enter a value", "Error",  MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}