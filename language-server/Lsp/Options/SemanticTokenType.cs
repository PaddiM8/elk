namespace Elk.LanguageServer.Lsp.Options;

public enum SemanticTokenType
{
    Namespace,
    Type,
    Class,
    Enum,
    Interface,
    Struct,
    TypeParameter,
    Parameter,
    Variable,
    Property,
    EnumMember,
    Event,
    Function,
    Method,
    Macro,
    Keyword,
    Modifier,
    Comment,
    String,
    Number,
    Regexp,
    Operator,
    Decorator,
}

static class SemanticTokenTypeExtensions
{
    public static string ToLspName(this SemanticTokenType tokenType)
    {
        var stringified = tokenType.ToString();
        var firstChar = stringified[0];

        return char.ToLower(firstChar) + stringified[1..];
    }
}