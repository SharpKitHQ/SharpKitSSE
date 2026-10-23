using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace SharpKit.Papyrus;

public static class PapyrusHighlighter
{
    public static IHighlightingDefinition Load()
    {
        var uri = new Uri(
            "/SharpKit;component/Papyrus/Papyrus.xshd",
            UriKind.Relative);

        using var stream = System.Windows.Application
                               .GetResourceStream(uri)?.Stream
                           ?? throw new FileNotFoundException(
                               "Papyrus.xshd resource not found.");

        using var reader = XmlReader.Create(stream);

        return HighlightingLoader.Load(
            reader,
            HighlightingManager.Instance);
    }
}