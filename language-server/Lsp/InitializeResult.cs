using Elk.LanguageServer.Lsp.Options;

namespace Elk.LanguageServer.Lsp;

public class InitializeResult
{
    public required ServerCapabilities Capabilities { get; set; }
}