using Elk.LanguageServer.Lsp.Documents;

namespace Elk.LanguageServer.Lsp.Items;

public class Diagnostic
{
    public required DocumentRange Range { get; set; }

    public DiagnosticSeverity? Severity { get; set; }

    public required string Message { get; set; }
}