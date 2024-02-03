using System;
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

[Flags]
public enum SemanticFeature
{
    None,
    TextArgumentCall,
}

public record SemanticToken(
    SemanticTokenKind Kind,
    string Value,
    TextPos Position
);