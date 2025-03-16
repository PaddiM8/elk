namespace Elk.LanguageServer.Lsp.Options;

public class SemanticTokensLegend
{
    public required IEnumerable<string> TokenTypes { get; set; }

    public IEnumerable<string> TokenModifiers { get; set; } = [];
}