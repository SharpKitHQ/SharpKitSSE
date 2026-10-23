using System.IO;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Microsoft.Extensions.DependencyInjection;
using Mutagen.Bethesda.Skyrim;
using Serilog;
using SharpKit.Papyrus;
using MessageBox = System.Windows.MessageBox;

namespace SharpKit.Windows.Data.FormEditors.Other;

public partial class ScriptEditor : Window
{
    public string ScriptSource { get; set; } = "";

    private CompletionWindow? _completionWindow;

    private static readonly string[] PapyrusKeywords = [
        "ScriptName", "Extends", "Import", "As", "Event", "EndEvent", "Function", "EndFunction", "Return", "Global",
        "Native", "Property", "EndProperty", "Auto", "AutoReadOnly", "Conditional", "Const", "If", "ElseIf", "Else",
        "EndIf", "While", "EndWhile", "State", "EndState", "Default", "New", "Parent", "Self", "Int", "Float", "Bool",
        "String", "Var", "True", "False", "None"
    ];

    private void InitAutocomplete()
    {
        CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering;
    }
    
    private void TextArea_TextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || _completionWindow is not null)
            return;
        
        if (!e.Text.Any(char.IsLetter))
            return;

        var caret = CodeEditor.CaretOffset;
        var document = CodeEditor.Document;
        var start = caret;

        while (start > 0)
        {
            var c = document.GetCharAt(start - 1);
            if (!char.IsLetterOrDigit(c) && c != '_')
                break;

            start--;
        }

        var prefix = document.GetText(start, caret - start);

        if (prefix.Length == 0)
            return;

        var matches = PapyrusKeywords
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
            return;

        _completionWindow = new CompletionWindow(CodeEditor.TextArea);
        _completionWindow.StartOffset = start;

        foreach (var keyword in matches)
            _completionWindow.CompletionList.CompletionData.Add(
                new PapyrusCompletionData(keyword));

        _completionWindow.Closed += (_, _) => _completionWindow = null;
        _completionWindow.Show();

    }

    private void TextArea_TextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (_completionWindow is not null &&
            e.Text.Length > 0 &&
            !char.IsLetterOrDigit(e.Text[0]) &&
            e.Text[0] != '\0')
        {
            _completionWindow.CompletionList.RequestInsertion(e);
        }
    }

    public ScriptEditor(IScriptEntryGetter script)
    {
        InitializeComponent();

        CodeEditor.SyntaxHighlighting = PapyrusHighlighter.Load();
        
        var env = App.ServiceProvider?.GetRequiredService<PluginFileManager>().GetEnv()
            ?? throw new Exception("App.ServiceProvider is null or there is no PluginFileManager registered");
        var dataDir = env.DataFolderPath.Path;
        var scriptSourceDir = Path.Combine(dataDir, "Source", "Scripts");

        var scriptFile = Path.Combine(scriptSourceDir, $"{script.Name}.psc");

        if (!File.Exists(scriptFile))
        {
            MessageBox.Show($"No such file {scriptFile}!", "File not found",  MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        Title = $"Editing script {scriptFile}";

        ScriptSource = File.ReadAllText(scriptFile);
        CodeEditor.Text = ScriptSource;

        CodeEditor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var line = CodeEditor.TextArea.Caret.Line;
            var col = CodeEditor.TextArea.Caret.Column;

            LineStatus.Content = $"Line {line}";
            ColumnStatus.Content = $"Column {col}";
        };

        foreach (var prop in script.Properties)
        {
            Log.Information("Prop {Name}, Flags: {Flags} ({Type})", prop.Name, prop.Flags, prop.GetType().FullName);
            foreach (var p in prop.GetType().GetProperties())
            {
                Log.Information(" - {PropName}", p.Name);
            }
        }
    }
}