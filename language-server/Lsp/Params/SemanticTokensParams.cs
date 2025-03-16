using Elk.LanguageServer.Lsp.Documents;

namespace Elk.LanguageServer.Lsp.Params;

public class SemanticTokensParams
{
    public required TextDocumentIdentifier TextDocument { get; set; }
}