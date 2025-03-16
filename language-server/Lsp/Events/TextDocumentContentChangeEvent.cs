namespace Elk.LanguageServer.Lsp.Events;

public class TextDocumentContentChangeEvent
{
    public required string Text { get; set; }
}