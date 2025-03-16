using Elk.LanguageServer.Lsp.Documents;

namespace Elk.LanguageServer.Lsp.Items;

public class Hover
{
    public required MarkedString Contents { get; set; }

    public DocumentRange? Range { get; set; }
}