using Elk.LanguageServer.Data;
using Elk.Parsing;
using Elk.Scoping;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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
                Range = new Range(
                    x.StartPosition.Line - 1,
                    x.StartPosition.Column - 1,
                    x.EndPosition.Line - 1,
                    x.EndPosition.Column - 1
                ),
                Message = x.Message,
                Severity = DiagnosticSeverity.Error,
            }
        );
    }

    public string? GetLineAtCaret(int line, int column)
    {
        var lineContent = Text.Split('\n').ElementAtOrDefault(line);
        if (lineContent == null || column > lineContent.Length)
            return null;

        return lineContent[..column];
    }
}
