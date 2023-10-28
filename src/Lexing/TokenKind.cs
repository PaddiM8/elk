#region

using System.ComponentModel;
using Elk.Parsing;

#endregion

namespace Elk.Lexing;

public enum TokenKind
{
    Unknown,

    // Keywords
    Not, And, Or, Fn, Let, If, Else, For, While, In, Return, Break, Continue, With, Using,
    Nil, True, False, Alias, Unalias, Module, Struct, New, Try, Catch, Throw, Pub,

    // Operators
    Plus, Minus, Star, Slash, Percent, Caret,
    Greater, Less, GreaterEquals, LessEquals, EqualsEquals, NotEquals,
    Equals, PlusEquals, MinusEquals, StarEquals, SlashEquals,
    AmpersandAmpersand, PipePipe,
    Pipe, PipeErr, PipeAll,
    Ampersand,
    Arrow,

    // Brackets
    OpenParenthesis, ClosedParenthesis,
    OpenSquareBracket, ClosedSquareBracket,
    OpenBrace, ClosedBrace,

    // Punctuation
    Comma, Colon, ColonColon, Dot, DotDot, Tilde, QuestionQuestion, Backslash, Semicolon,
    EqualsGreater,

    // Other
    IntegerLiteral, FloatLiteral, DoubleQuoteStringLiteral, SingleQuoteStringLiteral, BashLiteral,
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
            TokenKind.Not => OperationKind.Not,
            TokenKind.Pipe => OperationKind.Pipe,
            TokenKind.PipeErr => OperationKind.PipeErr,
            TokenKind.PipeAll => OperationKind.PipeAll,
            TokenKind.If => OperationKind.If,
            TokenKind.QuestionQuestion => OperationKind.Coalescing,
            TokenKind.AmpersandAmpersand => OperationKind.NonRedirectingAnd,
            TokenKind.PipePipe => OperationKind.NonRedirectingOr,
            TokenKind.In => OperationKind.In,
            _ => throw new InvalidEnumArgumentException(),
        };
    }
}