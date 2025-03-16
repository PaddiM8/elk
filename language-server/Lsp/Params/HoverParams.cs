using Elk.LanguageServer.Lsp.Documents;

namespace Elk.LanguageServer.Lsp.Params;

public class HoverParams
{
    public required TextDocumentIdentifier TextDocument { get; set; }

    public required Position Position { get; set; }
}