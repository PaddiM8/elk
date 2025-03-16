using Elk.LanguageServer.Lsp.Documents;
using Elk.LanguageServer.Lsp.Items;

namespace Elk.LanguageServer.Lsp.Params;

public class PublishDiagnosticsParams
{
    public required DocumentUri Uri { get; set; }

    public int? Version { get; set; }

    public required IEnumerable<Diagnostic> Diagnostics { get; set; }
}