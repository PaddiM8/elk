using System.Text.RegularExpressions;
using Elk.LanguageServer.Data;
using Elk.LanguageServer.Lsp;
using Elk.LanguageServer.Lsp.Documents;
using Elk.LanguageServer.Lsp.Items;
using Elk.Parsing;
using Elk.Scoping;

namespace Elk.LanguageServer;

class SemanticDocument(string uri, string text)
{
    public string Uri { get; } = uri;

    public string Text { get; set; } = text;

    public Ast? Ast { get; set; }

    public SemanticTokens SemanticTokens { get; set; } = new()
    {
        Data = [],
    };

    public IEnumerable<Diagnostic> Diagnostics { get; set; } = [];

    public ModuleScope Module { get; } = new RootModuleScope(uri, null);

    public void Update(string text)
    {
        Text = text;
    }

    public void RefreshSemantics()
    {
        var semanticResult = ElkProgram.GetSemanticInformation(Text, Module);
        if (semanticResult.Ast != null)
            Ast = semanticResult.Ast;

        if (semanticResult.SemanticTokens != null)
            SemanticTokens = TokenBuilder.BuildSemanticTokens(semanticResult.SemanticTokens);

        Diagnostics = semanticResult.Diagnostics.Select(x =>
            new Diagnostic
            {
                Range = new DocumentRange
                {
                    Start = new Position
                    {
                        Line = x.StartPosition.Line - 1,
                        Character = x.StartPosition.Column - 1,
                    },
                    End = new Position
                    {
                        Line = x.EndPosition.Line - 1,
                        Character = x.EndPosition.Column - 1,
                    },
                },
                Message = x.Message,
                Severity = DiagnosticSeverity.Error,
            }
        );
    }

    public string? GetLineAtCaret(int line, int column)
    {
        var lineContent = Regex.Split(Text, Environment.NewLine).ElementAtOrDefault(line);
        if (lineContent == null || column > lineContent.Length)
            return null;

        return lineContent[..column];
    }
}
