using Elk.LanguageServer.Lsp.Items;
using Elk.LanguageServer.Lsp.Options;
using Elk.Services;

namespace Elk.LanguageServer.Data;

static class TokenBuilder
{
    public static IEnumerable<string> GetSemanticTokenTypeLegend()
    {
        return Enum.GetValues<SemanticTokenType>()
            .Select(x => x.ToLspName());;
    }

    public static SemanticTokens BuildSemanticTokens(IEnumerable<SemanticToken> elkTokens)
    {
        var data = new List<int>();
        SemanticToken? previousToken = null;
        foreach (var token in elkTokens)
        {
            if (token.Kind is
                SemanticTokenKind.None or
                SemanticTokenKind.Keyword or
                SemanticTokenKind.Comment or
                SemanticTokenKind.Operator or
                SemanticTokenKind.InterpolationOperator)
                continue;

            var line = token.Position.Line - 1;
            var column = token.Position.Column - 1;
            var previousLine = previousToken?.Position.Line - 1;
            var previousColumn = previousToken?.Position.Column - 1;

            // Delta line
            data.Add(
                previousLine.HasValue
                    ? line - previousLine.Value
                    : line
            );

            // Delta start char
            data.Add(
                line == previousLine
                    ? column - previousColumn!.Value
                    : column
            );

            // Length
            data.Add(token.Value.Length);

            // Token type
            data.Add(ToLspTokenType(token.Kind));

            // Token modifiers
            data.Add(0);

            previousToken = token;
        }

        return new SemanticTokens
        {
            Data = [..data],
        };
    }

    private static int ToLspTokenType(SemanticTokenKind elkKind)
    {
        var lspType = elkKind switch
        {
            SemanticTokenKind.Module => SemanticTokenType.Namespace,
            SemanticTokenKind.Struct => SemanticTokenType.Struct,
            SemanticTokenKind.UnknownSymbol => SemanticTokenType.Variable,
            SemanticTokenKind.Type => SemanticTokenType.Type,
            SemanticTokenKind.Parameter => SemanticTokenType.Parameter,
            SemanticTokenKind.Variable => SemanticTokenType.Variable,
            SemanticTokenKind.Function => SemanticTokenType.Function,
            SemanticTokenKind.Keyword => SemanticTokenType.Keyword,
            SemanticTokenKind.Comment => SemanticTokenType.Comment,
            SemanticTokenKind.String => SemanticTokenType.String,
            SemanticTokenKind.TextArgument => SemanticTokenType.String,
            SemanticTokenKind.Path => SemanticTokenType.String,
            SemanticTokenKind.Number => SemanticTokenType.Number,
            SemanticTokenKind.Operator => SemanticTokenType.Operator,
            SemanticTokenKind.InterpolationOperator => SemanticTokenType.Operator,
            _ => throw new NotSupportedException(),
        };

        return (int)lspType;
    }
}
