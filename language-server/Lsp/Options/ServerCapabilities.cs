namespace Elk.LanguageServer.Lsp.Options;

public class ServerCapabilities
{
    public string PositionEncoding { get; set; } = "utf-16";

    public TextDocumentSyncKind? TextDocumentSync { get; set; }
    
    public CompletionOptions? CompletionProvider { get; set; }

    public SemanticTokensOptions? SemanticTokensProvider { get; set; }

    public bool HoverProvider { get; set; }
    
    public SignatureHelpOptions? SignatureHelpProvider { get; set; }

    public bool DefinitionProvider { get; set; }
}