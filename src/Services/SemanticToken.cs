using Elk.Lexing;

namespace Elk.Highlighting;

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

public record SemanticToken(SemanticTokenKind Kind, string Value, TextPos Position);