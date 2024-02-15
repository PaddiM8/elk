using Elk.Lexing;

namespace Elk.Services;

public enum SemanticTokenKind
{
    None,
    Module,
    UnknownSymbol,
    Type,
    Struct,
    Parameter,
    Variable,
    Function,
    Keyword,
    Comment,
    String,
    TextArgument,
    Path,
    Number,
    Operator,
    InterpolationOperator,
}

public record SemanticToken(
    SemanticTokenKind Kind,
    string Value,
    TextPos Position
)
{
    public SemanticToken(SemanticTokenKind kind, Token token)
        : this(kind, token.Value, token.Position)
    {
    }
}