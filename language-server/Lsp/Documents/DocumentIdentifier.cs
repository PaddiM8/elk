namespace Elk.LanguageServer.Lsp.Documents;

public class VersionedTextDocumentIdentifier : TextDocumentIdentifier
{
    public required int Version { get; set; }
}
