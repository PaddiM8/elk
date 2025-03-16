using Elk.LanguageServer.Lsp.Documents;
using Elk.LanguageServer.Lsp.Events;

namespace Elk.LanguageServer.Lsp.Params;

public class DidChangeTextDocumentParams
{
    public required VersionedTextDocumentIdentifier TextDocument { get; set; }

    public required IEnumerable<TextDocumentContentChangeEvent> ContentChanges { get; set; }
}