using System.ComponentModel;
using Elk.Parsing;

namespace Elk.Lexing;

enum TokenKind
{
    Unknown,

    // Keywords
    Fn, Let, If, Else, For, In, Return, Break, Continue, Include,
    Nil, True, False, Alias, Unalias,

    // Operators
    Plus, Minus, Star, Slash, Percent, Caret,
    Exclamation, Greater, Less, GreaterEquals, LessEquals, EqualsEquals, NotEquals,
    Equals, PlusEquals, MinusEquals, StarEquals, SlashEquals,
    And, Or,
    Pipe,

    // Brackets
    OpenParenthesis, ClosedParenthesis,
    OpenSquareBracket, ClosedSquareBracket,
    OpenBrace, ClosedBrace,

    // Punctuation
    Comma, Colon, Dot, DotDot, Tilde, QuestionQuestion, Backslash, Semicolon,

    // Other
    IntegerLiteral, FloatLiteral, StringLiteral,
    NewLine, WhiteSpace,
    Comment,
    Identifier,
    EndOfFile,
}

static class TokenKindExtensions
{
    public static OperationKind ToOperationKind(this TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Plus => OperationKind.Addition,
            TokenKind.Minus => OperationKind.Subtraction,
            TokenKind.Star => OperationKind.Multiplication,
            TokenKind.Slash => OperationKind.Division,
            TokenKind.Percent => OperationKind.Modulo,
            TokenKind.Caret => OperationKind.Power,
            TokenKind.Greater => OperationKind.Greater,
            TokenKind.GreaterEquals => OperationKind.GreaterEquals,
            TokenKind.Less => OperationKind.Less,
            TokenKind.LessEquals => OperationKind.LessEquals,
            TokenKind.Equals => OperationKind.Equals,
            TokenKind.EqualsEquals => OperationKind.EqualsEquals,
            TokenKind.NotEquals => OperationKind.NotEquals,
            TokenKind.And => OperationKind.And,
            TokenKind.Or => OperationKind.Or,
            TokenKind.Exclamation => OperationKind.Not,
            TokenKind.Pipe => OperationKind.Pipe,
            TokenKind.If => OperationKind.If,
            TokenKind.QuestionQuestion => OperationKind.Coalescing,
            _ => throw new InvalidEnumArgumentException(),
        };
    }
}