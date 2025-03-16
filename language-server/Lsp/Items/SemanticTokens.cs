namespace Elk.LanguageServer.Lsp.Items;

public class SemanticTokens
{
    public required IEnumerable<int> Data { get; set; }
}