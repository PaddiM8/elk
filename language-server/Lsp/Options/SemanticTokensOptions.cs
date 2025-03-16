namespace Elk.LanguageServer.Lsp.Options;

public class SemanticTokensOptions
{
    public required SemanticTokensLegend Legend { get; set; }

    public bool? Full { get; set; }
}