namespace Elk.LanguageServer.Lsp.Documents;

public class TextDocumentItem
{
    public required DocumentUri Uri { get; set; }

    public required string LanguageId { get; set; }

    public required int Version { get; set; }

    public required string  Text { get; set; }
}