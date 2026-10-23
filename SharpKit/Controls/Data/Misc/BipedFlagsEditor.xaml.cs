using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace SharpKit.Controls.Data.Misc;

public partial class BipedFlagsEditor : UserControl
{
    public sealed class FlagOption : INotifyPropertyChanged
    {
        public int BitIndex { get; }
        public int SlotNumber => BitIndex + 30;
        public string Name { get; }

        private bool _isChecked;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public FlagOption(int bitIndex, string name)
        {
            BitIndex = bitIndex;
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public ObservableCollection<FlagOption> Options { get; } = [];

    private bool _updatingOptions;

    public static readonly DependencyProperty FlagsProperty =
        DependencyProperty.Register(
            nameof(Flags),
            typeof(uint),
            typeof(BipedFlagsEditor),
            new FrameworkPropertyMetadata(
                0u,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnFlagsChanged));

    public uint Flags
    {
        get => (uint)GetValue(FlagsProperty);
        set => SetValue(FlagsProperty, value);
    }

    public BipedFlagsEditor()
    {
        InitializeComponent();

        var names = new Dictionary<int, string>
        {
            [0] = "Head",
            [1] = "Hair",
            [2] = "Body",
            [3] = "Hands",
            [4] = "Forearms",
            [5] = "Amulet",
            [6] = "Ring",
            [7] = "Feet",
            [8] = "Calves",
            [9] = "Shield",
            [10] = "Tail",
            [11] = "LongHair",
            [12] = "Circlet",
            [13] = "Ears",
            [20] = "DecapitateHead",
            [21] = "Decapitate",
            [31] = "FX01"
        };

        for (int i = 0; i < 32; i++)
        {
            var option = new FlagOption(
                i,
                names.GetValueOrDefault(i, "Unknown"));

            option.PropertyChanged += OnOptionChanged;
            Options.Add(option);
        }

        UpdateOptions(Flags);
    }

    private static void OnFlagsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (BipedFlagsEditor)d;
        control.UpdateOptions((uint)e.NewValue);
    }

    private void UpdateOptions(uint flags)
    {
        _updatingOptions = true;

        try
        {
            foreach (var option in Options)
            {
                uint mask = 1u << option.BitIndex;
                option.IsChecked = (flags & mask) != 0;
            }
        }
        finally
        {
            _updatingOptions = false;
        }
    }

    private void OnOptionChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (_updatingOptions ||
            e.PropertyName != nameof(FlagOption.IsChecked))
        {
            return;
        }

        uint value = Flags;

        foreach (var option in Options)
        {
            uint mask = 1u << option.BitIndex;

            if (option.IsChecked)
                value |= mask;
            else
                value &= ~mask;
        }

        Flags = value;
    }
}