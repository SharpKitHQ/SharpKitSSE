using System.Windows;
using Mutagen.Bethesda.Skyrim;

namespace SharpKit.Windows.Data.FormEditors.Other;

public partial class ScriptPropsEditor : Window
{
    private readonly List<IScriptPropertyGetter> _propGetters;
    
    public ScriptPropsEditor(IScriptEntryGetter script)
    {
        InitializeComponent();
        
        _propGetters = [.. script.Properties];

        Props.ItemsSource = _propGetters;
    }
}