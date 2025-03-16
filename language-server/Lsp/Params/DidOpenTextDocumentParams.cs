using Elk.LanguageServer.Lsp.Documents;

namespace Elk.LanguageServer.Lsp.Params;

public class DidOpenTextDocumentParams
{
    public required TextDocumentItem TextDocument { get; set; }
}