namespace Shel.Lexing;

enum TokenKind
{
    Unknown,

    // Keywords
    Fn, Let, If, Else, For, Return,
    Nil, True, False,

    // Operators
    Plus, Minus, Star, Slash,
    Exclamation, Greater, Less, GreaterEquals, LessEquals, EqualsEquals, NotEquals,
    Equals,
    And, Or,
    Pipe,

    // Brackets
    OpenParenthesis, ClosedParenthesis,
    OpenBrace, ClosedBrace,

    // Punctuation
    Comma, Colon, Hash,

    // Other
    NumberLiteral, StringLiteral,
    NewLine, WhiteSpace,
    Comment,
    Identifier,
    EndOfFile,
}